// ------------------------------------------------------------------
//  TopicFlowV2  –  activity manager / event-driven orchestrator
//  Same assembly as TopicFlowActivityV2
// ------------------------------------------------------------------
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.NEWCODE;

/// <summary>
/// TopicFlowV2 is the activity manager that:
/// - Owns the activity queue and manages sequencing
/// - Routes events between activities (internal bus)
/// - Forwards selected events to domain orchestrator (domain bus)
/// - Manages own lifecycle state
/// - Instantiates activities via reflection
/// - Activities are encapsulated - external entities never access them directly
/// </summary>
public class TopicFlowV2 : ITopicV2, ITerminable, IAsyncDisposable {
    // --------- EVENT ENVELOPE (DOMAIN) -----------------------------
    public record TopicEventEnvelope(
        string SourceId,
        string EventType,
        int Version,
        DateTime Timestamp,
        object? Payload
    );

    // --------- SCHEMA VERSION --------------------------------------
    internal const int SCHEMA_VERSION = 1;

    // --------- DEPENDENCIES ----------------------------------------
    private readonly string _name;
    private readonly IEventBus _domainEventBus;      // towards DomainAgent (InsuranceAgentService)
    private readonly IEventBus _internalEventBus;    // inside this topic (activities talk here)
    private readonly ILogger? _logger;
    private readonly TopicWorkflowContextV2 _context;  // shared mutable context

    // --------- ACTIVITY DESCRIPTORS (ADDED VIA BUILDER) ------------
    private readonly List<ActivityDescriptor> _descriptors = new();
    private readonly Dictionary<string, TopicFlowActivityV2> _running = new();

    // --------- RUNTIME STATE (PRIVATE) -----------------------------
    private TopicState _state = TopicState.Idle;
    private int _currentIndex = 0;
    private CancellationTokenSource? _linkedCts;

    // --------- CTOR (PUBLIC FOR DI) --------------------------------
    public TopicFlowV2(
        string name,
        IEventBus domainEventBus,
        ILogger? logger = null) {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _domainEventBus = domainEventBus ?? throw new ArgumentNullException(nameof(domainEventBus));
        _logger = logger;

        _context = new TopicWorkflowContextV2();
        _internalEventBus = new InMemoryEventBus();
        WireActivityForwarding();
    }

    // --------- BUILDER API (PUBLIC) --------------------------------
    /// <summary>
    /// Adds an activity descriptor to the execution queue.
    /// Activities are instantiated lazily during execution.
    /// </summary>
    public TopicFlowV2 Add<TActivity>(string id) where TActivity : TopicFlowActivityV2 {
        _descriptors.Add(new ActivityDescriptor(id, typeof(TActivity)));
        return this;
    }

    /// <summary>
    /// Inserts an activity after a specified anchor activity.
    /// </summary>
    public TopicFlowV2 AddBetween<TActivity>(string id, string afterId) where TActivity : TopicFlowActivityV2 {
        var idx = _descriptors.FindIndex(d => d.Id == afterId);
        if (idx < 0) throw new InvalidOperationException($"Anchor activity '{afterId}' not found.");
        _descriptors.Insert(idx + 1, new ActivityDescriptor(id, typeof(TActivity)));
        return this;
    }

    // --------- ITopicV2  (EXPLICIT IMPLEMENTATION) -----------------
    string ITopicV2.Name => _name;
    int ITopicV2.Priority => 0;   // override in derived topics if needed

    async Task ITopicV2.HandleAsync(string message, CancellationToken cancellationToken)
        => await StartAsync(message, cancellationToken);

    Task<float> ITopicV2.CanHandleAsync(string message, CancellationToken cancellationToken)
        => Task.FromResult(0.0f);   // override in derived topics

    // --------- PUBLIC API (DOMAIN ORCHESTRATOR) --------------------
    /// <summary>
    /// Starts the topic execution flow.
    /// This is the primary entry point called by InsuranceAgentService.
    /// </summary>
    public async Task StartAsync(object? initialInput = null, CancellationToken globalCt = default) {
        if (_state != TopicState.Idle) {
            PublishDomain(TopicEventType.TopicFailed,
                new { Reason = "Topic already started or completed" });
            return;
        }

        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
        await PumpAsync(initialInput, _linkedCts.Token);
    }

    /// <summary>
    /// Resumes topic execution after waiting for input.
    /// </summary>
    public async Task ResumeAsync(object? input, CancellationToken globalCt = default) {
        if (_state != TopicState.Waiting) {
            PublishDomain(TopicEventType.TopicFailed,
                new { Reason = "Resume called while not waiting" });
            return;
        }

        await PumpAsync(input, globalCt);
    }

    // --------- TERMINATION -----------------------------------------
    public bool IsTerminated { get; private set; }

    public async Task TerminateAsync() {
        if (IsTerminated) return;

        try {
            _linkedCts?.Cancel();
            await Task.WhenAll(_running.Values.Select(a => a.TerminateAsync()));
            PublishDomain(TopicEventType.TopicTerminated);
        } finally {
            IsTerminated = true;
            _linkedCts?.Dispose();
            _linkedCts = null;
        }
    }

    public ValueTask DisposeAsync() => new(TerminateAsync());

    // --------- PRIVATE PUMP (SEQUENTIAL EXECUTION) -----------------
    /// <summary>
    /// Core execution loop: instantiates activities, publishes execution requests,
    /// waits for completion, and advances the queue.
    /// </summary>
    private async Task PumpAsync(object? input, CancellationToken ct) {
        await TransitionToAsync(TopicState.Running, ct);
        PublishDomain(TopicEventType.TopicStarted);

        while (!ct.IsCancellationRequested && _currentIndex < _descriptors.Count) {
            var desc = _descriptors[_currentIndex];
            var activity = Instantiate(desc);   // creates & self-subscribes to internal bus
            _running[activity.Id] = activity;

            // CRITICAL FIX: Publish to INTERNAL bus (not domain bus!)
            // Activity subscribes to internal bus and filters by ActivityId
            await _internalEventBus.PublishAsync(new ActivityExecutionRequested(
                ActivityId: activity.Id,
                Input: input,
                ContextSnapshot: _context,  // Pass shared context (activities mutate it)
                CancellationToken: ct
            ), ct);

            // Wait for activity completion event
            var completed = await WaitActivityEventAsync(activity.Id,
                                  TopicFlowActivityV2.ActivityEventType.ActivityCompleted, ct);

            if (!completed) {
                await TransitionToAsync(TopicState.Failed, ct);
                PublishDomain(TopicEventType.TopicFailed, new { Reason = "Activity did not complete" });
                return;
            }

            _running.Remove(activity.Id);
            _currentIndex++;
            input = null; // subsequent activities start clean (context carries state)
        }

        await TransitionToAsync(TopicState.Completed, ct);
        PublishDomain(TopicEventType.TopicCompleted);
    }

    // --------- HELPERS ---------------------------------------------
    /// <summary>
    /// Instantiates an activity using reflection.
    /// Constructor signature: internal ctor(string id, IEventBus bus, ILogger? logger)
    /// </summary>
    private TopicFlowActivityV2 Instantiate(ActivityDescriptor desc) {
        var ctor = desc.Type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(IEventBus), typeof(ILogger) },
            modifiers: null);

        if (ctor == null)
            throw new InvalidOperationException(
                $"Activity {desc.Type.Name} missing ctor(string id, IEventBus bus, ILogger? logger)");

        return (TopicFlowActivityV2)ctor.Invoke(new object?[] { desc.Id, _internalEventBus, _logger });
    }

    /// <summary>
    /// Waits for a specific activity event to be published to the internal bus.
    /// Used for sequential flow control.
    /// </summary>
    private Task<bool> WaitActivityEventAsync(string activityId, string eventType, CancellationToken ct) {
        var tcs = new TaskCompletionSource<bool>();

        async Task RawHandler(object? raw, CancellationToken _) {
            if (raw is TopicFlowActivityV2.ActivityEventEnvelope e &&
                e.SourceId == activityId &&
                e.EventType == eventType) {
                (_internalEventBus as InMemoryEventBus)!.OnPublish -= RawHandler;
                tcs.TrySetResult(true);
            }
            await Task.CompletedTask; // suppress async warning
        }

        (_internalEventBus as InMemoryEventBus)!.OnPublish += RawHandler;
        ct.Register(() => tcs.TrySetCanceled());
        return tcs.Task;
    }

    /// <summary>
    /// Transitions topic state and publishes state change event to domain bus.
    /// </summary>
    private async Task TransitionToAsync(TopicState next, CancellationToken ct) {
        if (_state == next) return;
        var prev = _state;
        _state = next;
        _logger?.LogDebug("[TopicFlowV2:{Name}] {Prev} -> {Next}", _name, prev, next);
        PublishDomain(TopicEventType.TopicStateChanged,
            new { Previous = prev.ToString(), Next = next.ToString() });
        await Task.CompletedTask;
    }

    /// <summary>
    /// Wires internal bus to forward ALL activity events to domain bus.
    /// This is how InsuranceAgentService sees activity lifecycle without direct access.
    /// </summary>
    private void WireActivityForwarding() {
        _internalEventBus.OnPublish += async (envelope, _) => {
            if (envelope is TopicFlowActivityV2.ActivityEventEnvelope activityEnvelope) {
                // Forward activity events to domain bus with topic context
                PublishDomain(TopicEventType.ActivityForwarded, new {
                    ActivityId = activityEnvelope.SourceId,
                    EventType = activityEnvelope.EventType,
                    Payload = activityEnvelope.Payload,
                    Version = activityEnvelope.Version
                });
            }
            await Task.CompletedTask;
        };
    }

    /// <summary>
    /// Publishes an event to the domain bus (for InsuranceAgentService).
    /// This is fire-and-forget - no return values.
    /// </summary>
    private void PublishDomain(string eventType, object? payload = null) =>
        _domainEventBus.PublishAsync(new TopicEventEnvelope(
            SourceId: _name,
            EventType: eventType,
            Version: SCHEMA_VERSION,
            Timestamp: DateTime.UtcNow,
            Payload: payload), CancellationToken.None);

    // --------- DESCRIPTOR ------------------------------------------
    private record ActivityDescriptor(string Id, Type Type);

    // --------- STATE ENUM ------------------------------------------
    private enum TopicState { Idle, Running, Waiting, Completed, Failed, Terminated }

    // --------- EVENT TYPE CONSTANTS --------------------------------
    public static class TopicEventType {
        public const string TopicStarted = "Topic.Started";
        public const string TopicCompleted = "Topic.Completed";
        public const string TopicFailed = "Topic.Failed";
        public const string TopicTerminated = "Topic.Terminated";
        public const string TopicStateChanged = "Topic.StateChanged";
        public const string ActivityForwarded = "Topic.ActivityForwarded";
    }
}

/// <summary>
/// Interface for V2 topics.
/// Topics are event-driven orchestrators that NEVER return data - only publish events.
/// </summary>
public interface ITopicV2 {
    string Name { get; }
    int Priority { get; }

    /// <summary>
    /// Deliver user input to the topic.
    /// The topic MUST NOT return data; it MUST publish outcome events
    /// (TopicStarted, TopicCompleted, TopicFailed, etc.) through its domain bus.
    /// </summary>
    Task HandleAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confidence score (0-1) that this topic should react to the message.
    /// This method is allowed to be synchronous; it does not trigger events.
    /// </summary>
    Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default);
}
