//// ------------------------------------------------------------------
////  TopicFlowV2  –  internal / event-only / no-leak / no-return
////  Same assembly as TopicFlowActivityV2  →  visibility = internal
//// ------------------------------------------------------------------
//namespace ConversaCore.NEWCODE;
//using global::ConversaCore.TopicFlow;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Reflection;

//// ------------------------------------------------------------------
////  TopicFlowV2  –  internal / event-only / no-leak / implements ITopicV2
////  Lives in same assembly as TopicFlowActivityV2
//// ------------------------------------------------------------------
//public class TopicFlowV2 : ITopicV2, ITerminable, IAsyncDisposable {
//    // --------- EVENT ENVELOPE (INTERNAL) ---------------------------
//    internal record TopicEventEnvelope(
//        string SourceId,
//        string EventType,
//        int Version,
//        DateTime Timestamp,
//        object? Payload
//    );

//    // --------- SCHEMA VERSION --------------------------------------
//    internal const int SCHEMA_VERSION = 1;

//    // --------- DEPENDENCIES ----------------------------------------
//    private readonly string _name;
//    private readonly IEventBus _domainEventBus;      // towards DomainAgent
//    private readonly IEventBus _internalEventBus;    // inside this topic
//    private readonly ILogger? _logger;

//    // --------- ACTIVITY DESCRIPTORS (ADDED VIA BUILDER) ------------
//    private readonly List<ActivityDescriptor> _descriptors = new();
//    private readonly Dictionary<string, TopicFlowActivityV2> _running = new();

//    // --------- RUNTIME STATE (PRIVATE) -----------------------------
//    private TopicState _state = TopicState.Idle;
//    private int _currentIndex = 0;
//    private CancellationTokenSource? _linkedCts;

//    // --------- CTOR (PROTECTED FOR INHERITANCE) --------------------
//    public TopicFlowV2(
//        string name,
//        IEventBus domainEventBus,
//        ILogger? logger = null) {
//        _name = name;
//        _domainEventBus = domainEventBus ?? throw new ArgumentNullException(nameof(domainEventBus));
//        _logger = logger;

//        _internalEventBus = new InMemoryEventBus();
//        WireActivityForwarding();
//    }

//    // --------- BUILDER API (PUBLIC) --------------------------------
//    public TopicFlowV2 Add<TActivity>(string id) where TActivity : TopicFlowActivityV2 {
//        _descriptors.Add(new ActivityDescriptor(id, typeof(TActivity)));
//        return this;
//    }

//    public TopicFlowV2 AddBetween<TActivity>(string id, string afterId) where TActivity : TopicFlowActivityV2 {
//        var idx = _descriptors.FindIndex(d => d.Id == afterId);
//        if (idx < 0) throw new InvalidOperationException($"Anchor activity '{afterId}' not found.");
//        _descriptors.Insert(idx + 1, new ActivityDescriptor(id, typeof(TActivity)));
//        return this;
//    }

//    // --------- ITopicV2  (EXPLICIT) --------------------------------
//    string ITopicV2.Name => _name;

//    int ITopicV2.Priority => 0;   // override in derived topics if needed

//    async Task ITopicV2.HandleAsync(string message, CancellationToken cancellationToken)
//        => await PumpAsync(message, cancellationToken);

//    Task<float> ITopicV2.CanHandleAsync(string message, CancellationToken cancellationToken)
//        => Task.FromResult(0.0f);   // override in derived topics

//    // --------- LIFE-CYCLE (INTERNAL) -------------------------------
//    internal Task StartAsync(CancellationToken globalCt = default) {
//        if (_state != TopicState.Idle) {
//            PublishDomain(TopicEventType.TopicFailed,
//                new { Reason = "Topic already started or completed" });
//            return Task.CompletedTask;
//        }

//        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
//        return PumpAsync(null, _linkedCts.Token);
//    }

//    internal Task ResumeAsync(object? input, CancellationToken globalCt = default) {
//        if (_state != TopicState.Waiting) {
//            PublishDomain(TopicEventType.TopicFailed,
//                new { Reason = "Resume called while not waiting" });
//            return Task.CompletedTask;
//        }

//        return PumpAsync(input, globalCt);
//    }

//    // --------- TERMINATION -----------------------------------------
//    public bool IsTerminated { get; private set; }

//    public async Task TerminateAsync() {
//        if (IsTerminated) return;

//        try {
//            _linkedCts?.Cancel();
//            await Task.WhenAll(_running.Values.Select(a => a.TerminateAsync()));
//            PublishDomain(TopicEventType.TopicTerminated);
//        } finally {
//            IsTerminated = true;
//            _linkedCts?.Dispose();
//            _linkedCts = null;
//        }
//    }

//    public ValueTask DisposeAsync() => new(TerminateAsync());

//    // --------- PRIVATE PUMP (PURE EVENT) ---------------------------
//    private async Task PumpAsync(object? input, CancellationToken ct) {
//        await TransitionToAsync(TopicState.Running, ct);
//        PublishDomain(TopicEventType.TopicStarted);

//        while (!ct.IsCancellationRequested && _currentIndex < _descriptors.Count) {
//            var desc = _descriptors[_currentIndex];
//            var activity = Instantiate(desc);   // creates & wires subscription
//            _running[activity.Id] = activity;

//            // 1. BROADCAST request – activity will self-start
//            PublishDomain(TopicEventType.ActivityExecutionRequested,
//                new { ActivityId = activity.Id, Input = input, Context = new TopicWorkflowContext() });

//            // 2. WAIT for completion event (sequential flow)
//            var completed = await WaitActivityEventAsync(activity.Id,
//                                  TopicFlowActivityV2.ActivityEventType.ActivityCompleted, ct);
//            if (!completed) {
//                await TransitionToAsync(TopicState.Failed, ct);
//                PublishDomain(TopicEventType.TopicFailed, new { Reason = "Activity did not complete" });
//                return;
//            }

//            _running.Remove(activity.Id);
//            _currentIndex++;
//            input = null; // subsequent activities start clean
//        }

//        await TransitionToAsync(TopicState.Completed, ct);
//        PublishDomain(TopicEventType.TopicCompleted);
//    }

//    // --------- HELPERS ---------------------------------------------
//    private TopicFlowActivityV2 Instantiate(ActivityDescriptor desc) {
//        var ctor = desc.Type.GetConstructor(
//            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
//            binder: null,
//            types: new[] { typeof(string), typeof(IEventBus), typeof(ILogger) },
//            modifiers: null);

//        if (ctor == null)
//            throw new InvalidOperationException(
//                $"Activity {desc.Type.Name} missing ctor(string,IEventBus,ILogger)");

//        return (TopicFlowActivityV2)ctor.Invoke(new object?[] { desc.Id, _internalEventBus, _logger });
//    }

//    private Task<bool> WaitActivityEventAsync(string activityId, string eventType, CancellationToken ct) {
//        var tcs = new TaskCompletionSource<bool>();

//        async Task RawHandler(object? raw, CancellationToken _) {
//            if (raw is TopicFlowActivityV2.ActivityEventEnvelope e &&
//                e.SourceId == activityId &&
//                e.EventType == eventType) {
//                (_internalEventBus as InMemoryEventBus)!.OnPublish -= RawHandler;
//                tcs.TrySetResult(true);
//            }
//        }

//        (_internalEventBus as InMemoryEventBus)!.OnPublish += RawHandler;
//        ct.Register(() => tcs.TrySetCanceled());
//        return tcs.Task;
//    }

//    private async Task TransitionToAsync(TopicState next, CancellationToken ct) {
//        if (_state == next) return;
//        var prev = _state;
//        _state = next;
//        PublishDomain(TopicEventType.TopicStateChanged,
//            new { Previous = prev.ToString(), Next = next.ToString() });
//        await Task.CompletedTask;
//    }

//    private void WireActivityForwarding() {
//        _internalEventBus.OnPublish += async (envelope, _) => {
//            PublishDomain(TopicEventType.ActivityForwarded, new {
//                ActivityId = ((TopicFlowActivityV2.ActivityEventEnvelope)envelope).SourceId,
//                ((TopicFlowActivityV2.ActivityEventEnvelope)envelope).EventType,
//                ((TopicFlowActivityV2.ActivityEventEnvelope)envelope).Payload,
//                ((TopicFlowActivityV2.ActivityEventEnvelope)envelope).Version
//            });
//        };
//    }

//    private void PublishDomain(string eventType, object? payload = null) =>
//        _domainEventBus.PublishAsync(new TopicEventEnvelope(
//            SourceId: _name,
//            EventType: eventType,
//            Version: SCHEMA_VERSION,
//            Timestamp: DateTime.UtcNow,
//            Payload: payload), CancellationToken.None);

//    // --------- DESCRIPTOR ------------------------------------------
//    private record ActivityDescriptor(string Id, Type Type);

//    // --------- STATE ENUM ------------------------------------------
//    private enum TopicState { Idle, Running, Waiting, Completed, Failed, Terminated }

//    // --------- EVENT TYPE CONSTANTS --------------------------------
//    internal static class TopicEventType {
//        public const string TopicStarted = "Topic.Started";
//        public const string TopicCompleted = "Topic.Completed";
//        public const string TopicFailed = "Topic.Failed";
//        public const string TopicTerminated = "Topic.Terminated";
//        public const string TopicStateChanged = "Topic.StateChanged";
//        public const string ActivityForwarded = "Topic.ActivityForwarded";
//        public const string ActivityExecutionRequested = "Topic.ActivityExecutionRequested";
//    }
//}

//internal interface ITopicV2 {
//    string Name { get; }
//    int Priority { get; }

//    /// <summary>
//    /// Deliver user input to the topic.
//    /// The topic MUST NOT return data; it MUST publish outcome events
//    /// (TopicStarted, TopicCompleted, TopicFailed, etc.) through its internal bus.
//    /// </summary>
//    Task HandleAsync(string message, CancellationToken cancellationToken = default);

//    /// <summary>
//    /// Confidence score (0-1) that this topic should react to the message.
//    /// This method is allowed to be synchronous; it does not trigger events.
//    /// </summary>
//    Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default);
//}

