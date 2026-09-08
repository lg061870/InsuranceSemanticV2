using System.Threading.Channels;
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
    private readonly ILogger<LegacyTopicOutputAdapter> _logger;

    /// <summary>Creates a scoped compatibility adapter.</summary>
    public LegacyTopicOutputAdapter(IConversationSession session, IConversationOutputDispatcher dispatcher,
        ILogger<LegacyTopicOutputAdapter> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IAsyncDisposable Attach(TopicDescriptor descriptor, ITopic topic)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(topic);
        return topic is Flow flow
            ? new FlowLease(_session.ConversationId, descriptor.TopicId, flow, _dispatcher, _logger)
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
        private readonly ILogger _logger;
        private readonly Channel<ConversationOutput> _pending;
        private readonly HashSet<TopicFlowActivity> _activities = [];
        private readonly Task _pump;
        private string? _activeCardId;
        private int _disposed;

        public FlowLease(string conversationId, string topicId, Flow flow,
            IConversationOutputDispatcher dispatcher, ILogger logger)
        {
            _conversationId = conversationId;
            _topicId = topicId;
            _flow = flow;
            _dispatcher = dispatcher;
            _logger = logger;
            _pending = Channel.CreateUnbounded<ConversationOutput>(new UnboundedChannelOptions
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
            }
        }

        private void Publish(ConversationOutput output)
        {
            if (Volatile.Read(ref _disposed) == 0 && !_pending.Writer.TryWrite(output))
                _logger.LogWarning("Legacy output for topic {TopicId} could not be queued", _topicId);
        }

        private async Task PumpAsync()
        {
            await foreach (var output in _pending.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    await _dispatcher.DispatchAsync(output).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
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
