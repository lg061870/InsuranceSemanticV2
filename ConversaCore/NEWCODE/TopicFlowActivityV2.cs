//// ------------------------------------------------------------------
////  TopicFlowActivity  –  internal / event-only / no-return
////  Same assembly as TopicFlow  →  visibility = internal
//// ------------------------------------------------------------------
//using ConversaCore.Events;
//using ConversaCore.TopicFlow;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;

//namespace ConversaCore.NEWCODE;

//// ------------------------------------------------------------------
////  TopicFlowActivityV2  –  pure duplex / no inbound API / event-only
////  Same assembly as TopicFlowV2  →  visibility = internal
//// ------------------------------------------------------------------

//public class TopicFlowActivityV2 : ITerminable, IAsyncDisposable {
//    // --------- EVENT ENVELOPE (INTERNAL) ---------------------------
//    internal record ActivityEventEnvelope(
//        string SourceId,
//        string EventType,
//        int Version,
//        DateTime Timestamp,
//        object? Payload
//    );

//    // --------- SCHEMA VERSION --------------------------------------
//    internal const int SCHEMA_VERSION = 1;

//    // --------- DEPENDENCIES ----------------------------------------
//    private readonly string _id;
//    private readonly IEventBus _bus;          // TopicFlow's internal bus
//    private readonly ILogger? _logger;
//    private CancellationTokenSource? _linkedCts;

//    // --------- STATE (PRIVATE) -------------------------------------
//    private ActivityState _state = ActivityState.Idle;
//    private bool _subscribed;                 // avoid double subscription

//    // --------- INTERNAL CTOR (SAME ASSEMBLY) -----------------------
//    internal TopicFlowActivityV2(
//        string id,
//        IEventBus bus,
//        ILogger? logger = null) {
//        _id = id;
//        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
//        _logger = logger;

//        // SELF-SUBSCRIBE to the bus for duplex start
//        _bus.Subscribe<ActivityExecutionRequested>(OnExecutionRequest);
//        _subscribed = true;
//    }

//    // --------- PUBLIC IDENTIFIER -----------------------------------
//    internal string Id => _id;

//    // --------- TERMINATION -----------------------------------------
//    public bool IsTerminated { get; private set; }

//    public async Task TerminateAsync() {
//        if (IsTerminated) return;

//        try {
//            _linkedCts?.Cancel();
//            await PublishAsync(ActivityEventType.ActivityTerminated);
//        } finally {
//            IsTerminated = true;
//            _linkedCts?.Dispose();
//            _linkedCts = null;

//            if (_subscribed) {
//                _bus.Unsubscribe<ActivityExecutionRequested>(OnExecutionRequest);
//                _subscribed = false;
//            }
//        }
//    }

//    public ValueTask DisposeAsync() => new(TerminateAsync());

//    // --------- DUPLEX START HANDLER --------------------------------
//    private async Task OnExecutionRequest(ActivityExecutionRequested req) {
//        if (req.ActivityId != _id) return;        // not mine
//        if (_state != ActivityState.Idle) return; // already running

//        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(req.CancellationToken);
//        await RunSelfAsync(req.Input, req.ContextSnapshot, _linkedCts.Token);
//    }

//    // --------- PRIVATE RUN-SELF (NO INBOUND API) -------------------
//    private async Task RunSelfAsync(object? input, TopicWorkflowContextV2 ctx, CancellationToken ct) {
//        try {
//            await TransitionToAsync(ActivityState.Running, ct);
//            await PublishAsync(ActivityEventType.ActivityStarted,
//                new { Input = input, ContextKeys = ctx.GetKeys() });

//            // ---- DOMAIN WORK -------------------------------------
//            await ExecuteCoreAsync(ctx, input, ct);

//            await TransitionToAsync(ActivityState.Completed, ct);
//            await PublishAsync(ActivityEventType.ActivityCompleted,
//                new { ContextKeys = ctx.GetKeys() });
//        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
//            await TransitionToAsync(ActivityState.TimedOut, ct);
//            await PublishAsync(ActivityEventType.ActivityTimedOut);
//        } catch (Exception ex) {
//            await TransitionToAsync(ActivityState.Failed, ct);
//            await PublishAsync(ActivityEventType.ActivityFailed,
//                new { Reason = ex.Message, ExceptionType = ex.GetType().Name });
//        }
//    }

//    // --------- DOMAIN WORK (IMPLEMENTED BY DERIVED TYPES) ----------
//    private async Task ExecuteCoreAsync(TopicWorkflowContextV2 ctx, object? input, CancellationToken ct) {
//        // default: no-op
//        await Task.CompletedTask;
//    }

//    // --------- HELPERS ---------------------------------------------
//    private async Task TransitionToAsync(ActivityState next, CancellationToken ct) {
//        if (_state == next) return;
//        var prev = _state;
//        _state = next;
//        _logger?.LogDebug("[{Id}] {Prev} -> {Next}", _id, prev, next);
//        await PublishAsync(ActivityEventType.ActivityStateChanged,
//            new { Previous = prev.ToString(), Next = next.ToString() });
//    }

//    internal Task PublishAsync(string eventType, object? payload = null) =>
//        _bus.PublishAsync(new ActivityEventEnvelope(
//            SourceId: _id,
//            EventType: eventType,
//            Version: SCHEMA_VERSION,
//            Timestamp: DateTime.UtcNow,
//            Payload: payload), CancellationToken.None);

//    // --------- EVENT TYPE CONSTANTS --------------------------------
//    internal static class ActivityEventType {
//        public const string ActivityStarted = "Activity.Started";
//        public const string ActivityCompleted = "Activity.Completed";
//        public const string ActivityFailed = "Activity.Failed";
//        public const string ActivityTimedOut = "Activity.TimedOut";
//        public const string ActivityTerminated = "Activity.Terminated";
//        public const string ActivityStateChanged = "Activity.StateChanged";
//        public const string ActivityWaiting = "Activity.Waiting";
//        public const string ActivityResumed = "Activity.Resumed";
//    }

//    // --------- STATE ENUM ------------------------------------------
//    private enum ActivityState { Idle, Running, Completed, Failed, TimedOut, Terminated }
//}

//// --------- INBOUND REQUEST EVENT (SAME ASSEMBLY) -----------------
//public record ActivityExecutionRequested(
//    string ActivityId,
//    object? Input,
//    TopicWorkflowContextV2 ContextSnapshot,
//    CancellationToken CancellationToken
//);

//// --------- SAME-ASSEMBLY INTERFACE -------------------------------
//public interface IRunnableActivity {
//    string Id { get; }
//    Task RunAsync(TopicWorkflowContextV2 context, object? input, CancellationToken globalCt);
//    Task TerminateAsync();
//}

//// ------------------------------------------------------------------
////  ITerminable  –  unchanged, assembly-neutral
//// ------------------------------------------------------------------
//public interface ITerminable {
//    bool IsTerminated { get; }
//    Task TerminateAsync();
//}

//// ------------------------------------------------------------------
////  IEventBus  –  minimal, internal to TopicFlow assembly
//// ------------------------------------------------------------------
//public interface IEventBus {
//    Task PublishAsync<T>(T envelope, CancellationToken ct = default);
//    void Subscribe<T>(Func<T, Task> handler);
//    void Unsubscribe<T>(Func<T, Task> handler);

//    // raw stream for same-assembly consumers
//    event Func<object, CancellationToken, Task>? OnPublish;
//}

//// ------------------------------------------------------------------
////  InMemoryEventBus  –  same-assembly, thread-unsafe, good enough for V1
//// ------------------------------------------------------------------
//public sealed class InMemoryEventBus : IEventBus {
//    // publish side
//    public event Func<object, CancellationToken, Task>? OnPublish;

//    // subscribe side
//    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

//    // ----------  PUBLISH  ------------------------------------------
//    async Task IEventBus.PublishAsync<T>(T envelope, CancellationToken ct) {
//        var handler = OnPublish;
//        if (handler != null) await handler(envelope, ct);

//        // also raise to typed subscribers
//        if (_handlers.TryGetValue(typeof(T), out var list)) {
//            foreach (var d in list)
//                await ((Func<T, Task>)d)(envelope);
//        }
//    }

//    // ----------  SUBSCRIBE / UNSUBSCRIBE  --------------------------
//    public void Subscribe<T>(Func<T, Task> handler) {
//        var key = typeof(T);
//        if (!_handlers.TryGetValue(key, out var list)) {
//            list = new List<Delegate>();
//            _handlers[key] = list;
//        }
//        list.Add(handler);
//    }

//    public void Unsubscribe<T>(Func<T, Task> handler) {
//        var key = typeof(T);
//        if (_handlers.TryGetValue(key, out var list))
//            list.Remove(handler);
//    }


 
//}

///// <summary>
///// A key-value store for workflow state.
///// </summary>
//public class TopicWorkflowContextV2 {
//    // (keep only one declaration below)

//    // DEBUG: Tracking Context Lifecycle
//    public override string ToString() {
//        var sb = new System.Text.StringBuilder();
//        foreach (var kvp in _values) {
//            sb.Append(kvp.Key);
//            sb.Append(": ");
//            if (kvp.Value is string s)
//                sb.Append(s);
//            else if (kvp.Value != null)
//                sb.Append(kvp.Value.GetType().ToString());
//            else
//                sb.Append("null");
//            sb.AppendLine();
//        }
//        return sb.ToString();
//    }
//    private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

//    /// <summary>
//    /// Sets a value in the workflow context.
//    /// </summary>
//    /// <param name="key">The key to store the value under.</param>
//    /// <param name="value">The value to store.</param>
//    public void SetValue(string key, object? value) {
//        if (string.IsNullOrEmpty(key))
//            throw new ArgumentNullException(nameof(key));

//        if (value == null) {
//            if (_values.ContainsKey(key))
//                _values.Remove(key);
//            return;
//        }

//        _values[key] = value;
//    }

//    /// <summary>
//    /// Gets a value from the workflow context.
//    /// </summary>
//    /// <typeparam name="T">The type to convert the value to.</typeparam>
//    /// <param name="key">The key to retrieve the value for.</param>
//    /// <returns>The value if found and convertible to T; default(T) otherwise.</returns>
//    public T? GetValue<T>(string key) {
//        if (string.IsNullOrEmpty(key) || !_values.ContainsKey(key))
//            return default;

//        var value = _values[key];

//        if (value is T typedValue)
//            return typedValue;

//        try {
//            // 🔹 Handle JsonElement (common in JSON-based contexts)
//            if (value is JsonElement jsonElement) {
//                object? unwrapped = jsonElement.ValueKind switch {
//                    JsonValueKind.String => jsonElement.GetString(),
//                    JsonValueKind.Number => jsonElement.TryGetInt64(out var i64) ? i64 : jsonElement.TryGetDouble(out var dbl) ? dbl : (object?)null,
//                    JsonValueKind.True => true,
//                    JsonValueKind.False => false,
//                    JsonValueKind.Null or JsonValueKind.Undefined => null,
//                    _ => jsonElement.ToString()
//                };

//                // Convert again if needed
//                if (unwrapped is T direct)
//                    return direct;

//                if (unwrapped != null)
//                    return (T)Convert.ChangeType(unwrapped, typeof(T));
//            }

//            // Fallback: normal conversion
//            return (T)Convert.ChangeType(value, typeof(T));
//        } catch {
//            return default;
//        }
//    }


//    /// <summary>
//    /// Gets a value from the workflow context or a default value if not found.
//    /// </summary>
//    /// <typeparam name="T">The type to convert the value to.</typeparam>
//    /// <param name="key">The key to retrieve the value for.</param>
//    /// <param name="defaultValue">The default value to return if the key is not found.</param>
//    /// <returns>The value if found and convertible to T; defaultValue otherwise.</returns>
//    public T GetValue<T>(string key, T defaultValue) {
//        var value = GetValue<T>(key);
//        return value != null ? value : defaultValue;
//    }

//    /// <summary>
//    /// Checks if the workflow context contains a specific key.
//    /// </summary>
//    /// <param name="key">The key to check for.</param>
//    /// <returns>True if the key exists; false otherwise.</returns>
//    public bool ContainsKey(string key) {
//        return !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
//    }

//    /// <summary>
//    /// Gets all keys in the workflow context.
//    /// </summary>
//    /// <returns>An enumerable of all keys.</returns>
//    public IEnumerable<string> GetKeys() {
//        return _values.Keys;
//    }

//    /// <summary>
//    /// Clears all values from the workflow context.
//    /// </summary>
//    public void Clear() {
//        _values.Clear();
//    }

//    public void RemoveValue(string key) {
//        if (string.IsNullOrEmpty(key)) return;
//        _values.Remove(key);
//    }


//    /// <summary>
//    /// Gets the number of items in the workflow context.
//    /// </summary>
//    public int Count => _values.Count;
//}