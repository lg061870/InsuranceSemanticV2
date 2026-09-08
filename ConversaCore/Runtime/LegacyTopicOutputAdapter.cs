using System.Threading.Channels;
using System.Text.Json;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Runtime;

/// <summary>Default adapter from legacy <see cref="Flow"/> events to typed conversation output.</summary>
public sealed class LegacyTopicOutputAdapter : ILegacyTopicOutputAdapter
{
    private readonly IConversationSession _session;
    private readonly IConversationOutputDispatcher _dispatcher;
    private readonly IHostInteractionCoordinator? _interactionCoordinator;
    private readonly ILogger<LegacyTopicOutputAdapter> _logger;

    /// <summary>Creates a scoped compatibility adapter.</summary>
    public LegacyTopicOutputAdapter(IConversationSession session, IConversationOutputDispatcher dispatcher,
        ILogger<LegacyTopicOutputAdapter> logger, IHostInteractionCoordinator? interactionCoordinator = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interactionCoordinator = interactionCoordinator;
    }

    /// <inheritdoc />
    public IAsyncDisposable Attach(TopicDescriptor descriptor, ITopic topic)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(topic);
        return topic is Flow flow
            ? new FlowLease(_session.ConversationId, descriptor.TopicId, flow, _dispatcher,
                _interactionCoordinator, _logger)
            : EmptyLease.Instance;
    }

    private sealed class EmptyLease : IAsyncDisposable
    {
        public static EmptyLease Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FlowLease : IAsyncDisposable
    {
        private readonly string _conversationId;
        private readonly string _topicId;
        private readonly Flow _flow;
        private readonly IConversationOutputDispatcher _dispatcher;
        private readonly IHostInteractionCoordinator? _interactionCoordinator;
        private readonly ILogger _logger;
        private readonly Channel<PendingDispatch> _pending;
        private readonly HashSet<TopicFlowActivity> _activities = [];
        private readonly Dictionary<EventTriggerActivity, IDisposable> _eventTriggerLeases = [];
        private readonly Task _pump;
        private string? _activeCardId;
        private int _disposed;

        public FlowLease(string conversationId, string topicId, Flow flow,
            IConversationOutputDispatcher dispatcher, IHostInteractionCoordinator? interactionCoordinator,
            ILogger logger)
        {
            _conversationId = conversationId;
            _topicId = topicId;
            _flow = flow;
            _dispatcher = dispatcher;
            _interactionCoordinator = interactionCoordinator;
            _logger = logger;
            _pending = Channel.CreateUnbounded<PendingDispatch>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });
            _flow.TopicLifecycleChanged += OnTopicLifecycleChanged;
            _flow.ActivityCreated += OnActivityCreated;
            HookNewActivities();
            _pump = PumpAsync();
        }

        private void OnTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e)
        {
            HookNewActivities();
            Publish(new TopicLifecycleOutput(_conversationId, _topicId, Map(e.State), SafeDetail(e.Data)));
        }

        private void OnActivityCreated(object? sender, ActivityCreatedEventArgs e)
        {
            HookNewActivities();
        }

        private void OnActivityLifecycleChanged(object? sender, ActivityLifecycleEventArgs e) =>
            Publish(new ActivityLifecycleOutput(_conversationId, _topicId, e.ActivityId,
                Map(e.State), SafeDetail(e.Data)));

        private void OnMessageEmitted(object? sender, MessageEmittedEventArgs e) =>
            Publish(new MessageOutput(_conversationId, e.Message));

        private void OnCardJsonSent(object? sender, CardJsonEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_activeCardId) &&
                !string.Equals(_activeCardId, e.CardId, StringComparison.Ordinal))
                Publish(new CardStateOutput(_conversationId, _activeCardId, ConversationCardState.ReadOnly));

            _activeCardId = e.CardId;
            Publish(new CardStateOutput(_conversationId, e.CardId, ConversationCardState.Active));
            Publish(new AdaptiveCardOutput(_conversationId, e.CardId, e.CardJson,
                e.RenderMode == RenderMode.Replace
                    ? ConversationCardRenderMode.Replace
                    : ConversationCardRenderMode.Append,
                e.IsRequired));
            Publish(new PromptStateOutput(_conversationId,
                e.IsRequired ? ConversationPromptState.Disabled : ConversationPromptState.Enabled, e.CardId));
        }

        private void HookNewActivities()
        {
            foreach (var activity in _flow.GetAllActivities())
            {
                if (!_activities.Add(activity)) continue;
                activity.ActivityLifecycleChanged += OnActivityLifecycleChanged;
                activity.MessageEmitted += OnMessageEmitted;
                if (activity is IAdaptiveCardActivity card)
                    card.CardJsonSent += OnCardJsonSent;
                if (activity is EventTriggerActivity eventTrigger)
                    _eventTriggerLeases.Add(eventTrigger,
                        eventTrigger.AttachRuntimeHandler((eventName, data, waitForResponse, timeout, cancellationToken) =>
                            RouteLegacyEventAsync(eventTrigger.Id, eventName, data, waitForResponse, timeout,
                                cancellationToken)));
            }
        }

        private async Task<object?> RouteLegacyEventAsync(string activityId, string eventName, object? data,
            bool waitForResponse, TimeSpan timeout, CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "Legacy EventTriggerActivity {ActivityId} in topic {TopicId} used event {EventName}; " +
                "replace it with a typed host contract",
                activityId, _topicId, eventName);

            var payload = LegacyEventTriggerPayload.Create(activityId, data);
            if (!waitForResponse)
            {
                await PublishAsync(
                    new HostNotification<LegacyEventTriggerPayload>(_conversationId, eventName, 1, payload),
                    cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (_interactionCoordinator is null)
                throw new InvalidOperationException(
                    "Wait-for-response EventTriggerActivity compatibility requires IHostInteractionCoordinator.");

            await FlushAsync(cancellationToken).ConfigureAwait(false);
            return await _interactionCoordinator
                .RequestAsync<LegacyEventTriggerPayload, JsonElement>(
                    eventName, 1, payload, timeout, cancellationToken)
                .ConfigureAwait(false);
        }

        private void Publish(ConversationOutput output)
        {
            if (Volatile.Read(ref _disposed) == 0 && !_pending.Writer.TryWrite(new PendingDispatch(output)))
                _logger.LogWarning("Legacy output for topic {TopicId} could not be queued", _topicId);
        }

        private Task PublishAsync(ConversationOutput output, CancellationToken cancellationToken) =>
            QueueAndWaitAsync(output, cancellationToken);

        private Task FlushAsync(CancellationToken cancellationToken) =>
            QueueAndWaitAsync(null, cancellationToken);

        private Task QueueAndWaitAsync(ConversationOutput? output, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.Writer.TryWrite(new PendingDispatch(output, completion, cancellationToken)))
                throw new ObjectDisposedException(nameof(FlowLease));
            return completion.Task;
        }

        private async Task PumpAsync()
        {
            await foreach (var item in _pending.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (item.Output is not null)
                        await _dispatcher.DispatchAsync(item.Output, item.CancellationToken).ConfigureAwait(false);
                    item.Completion?.TrySetResult();
                }
                catch (Exception ex)
                {
                    if (item.Completion is not null)
                        item.Completion.TrySetException(ex);
                    else
                        _logger.LogError(ex, "Failed to dispatch legacy output for topic {TopicId}", _topicId);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _flow.TopicLifecycleChanged -= OnTopicLifecycleChanged;
            _flow.ActivityCreated -= OnActivityCreated;
            foreach (var activity in _activities)
            {
                activity.ActivityLifecycleChanged -= OnActivityLifecycleChanged;
                activity.MessageEmitted -= OnMessageEmitted;
                if (activity is IAdaptiveCardActivity card)
                    card.CardJsonSent -= OnCardJsonSent;
            }
            foreach (var lease in _eventTriggerLeases.Values) lease.Dispose();
            _eventTriggerLeases.Clear();
            _activities.Clear();
            _pending.Writer.TryComplete();
            await _pump.ConfigureAwait(false);
        }

        private static string? SafeDetail(object? data) => data switch
        {
            string detail => detail,
            Exception exception => exception.GetType().Name,
            _ => null
        };

        private sealed record PendingDispatch(
            ConversationOutput? Output,
            TaskCompletionSource? Completion = null,
            CancellationToken CancellationToken = default);

        private static ConversationTopicState Map(TopicLifecycleState state) => state switch
        {
            TopicLifecycleState.Created => ConversationTopicState.Created,
            TopicLifecycleState.Starting => ConversationTopicState.Starting,
            TopicLifecycleState.Running => ConversationTopicState.Running,
            TopicLifecycleState.WaitingForUserInput => ConversationTopicState.WaitingForInput,
            TopicLifecycleState.WaitingForSubTopic => ConversationTopicState.WaitingForSubtopic,
            TopicLifecycleState.Resuming => ConversationTopicState.Resuming,
            TopicLifecycleState.Completed => ConversationTopicState.Completed,
            TopicLifecycleState.Failed => ConversationTopicState.Failed,
            TopicLifecycleState.Cancelled => ConversationTopicState.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        private static ConversationActivityState Map(ActivityState state) => state switch
        {
            ActivityState.Idle => ConversationActivityState.Idle,
            ActivityState.Created => ConversationActivityState.Created,
            ActivityState.Running => ConversationActivityState.Running,
            ActivityState.Rendered => ConversationActivityState.Rendered,
            ActivityState.WaitingForUserInput => ConversationActivityState.WaitingForInput,
            ActivityState.WaitingForSubActivity => ConversationActivityState.WaitingForSubactivity,
            ActivityState.InputCollected => ConversationActivityState.InputCollected,
            ActivityState.ValidationFailed => ConversationActivityState.ValidationFailed,
            ActivityState.Triggered => ConversationActivityState.Triggered,
            ActivityState.Completed => ConversationActivityState.Completed,
            ActivityState.Failed => ConversationActivityState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
