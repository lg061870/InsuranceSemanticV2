// ------------------------------------------------------------------
//  TopicFlowActivityV2  –  pure event-driven / autonomous / extensible
//  Same assembly as TopicFlowV2  →  visibility = internal
// ------------------------------------------------------------------
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.NEWCODE;

/// <summary>
/// Base class for all activities in TopicFlowV2.
/// Activities are autonomous, event-driven nodes that:
/// - Self-subscribe to internal event bus on construction
/// - Listen for ActivityExecutionRequested events
/// - Manage their own state transitions
/// - Publish lifecycle events (Started, Completed, Failed, etc.)
/// - Have NO public RunAsync() - only internal event-driven execution
/// </summary>
public class TopicFlowActivityV2 : ITerminable, IAsyncDisposable {
    // --------- EVENT ENVELOPE (INTERNAL) ---------------------------
    internal record ActivityEventEnvelope(
        string SourceId,
        string EventType,
        int Version,
        DateTime Timestamp,
        object? Payload
    );

    // --------- SCHEMA VERSION --------------------------------------
    internal const int SCHEMA_VERSION = 1;

    // --------- DEPENDENCIES ----------------------------------------
    private readonly string _id;
    private readonly IEventBus _bus;          // TopicFlow's internal bus
    private readonly ILogger? _logger;
    private CancellationTokenSource? _linkedCts;

    // --------- STATE (PRIVATE) -------------------------------------
    private ActivityState _state = ActivityState.Idle;
    private bool _subscribed;                 // avoid double subscription

    // --------- INTERNAL CTOR (SAME ASSEMBLY) -----------------------
    internal TopicFlowActivityV2(
        string id,
        IEventBus bus,
        ILogger? logger = null) {
        _id = id ?? throw new ArgumentNullException(nameof(id));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _logger = logger;

        // SELF-SUBSCRIBE to the bus for duplex start
        _bus.Subscribe<ActivityExecutionRequested>(OnExecutionRequest);
        _subscribed = true;
    }

    // --------- PUBLIC IDENTIFIER -----------------------------------
    public string Id => _id;

    // --------- TERMINATION -----------------------------------------
    public bool IsTerminated { get; private set; }

    public async Task TerminateAsync() {
        if (IsTerminated) return;

        try {
            _linkedCts?.Cancel();
            await PublishAsync(ActivityEventType.ActivityTerminated);
        } finally {
            IsTerminated = true;
            _linkedCts?.Dispose();
            _linkedCts = null;

            if (_subscribed) {
                _bus.Unsubscribe<ActivityExecutionRequested>(OnExecutionRequest);
                _subscribed = false;
            }
        }
    }

    public ValueTask DisposeAsync() => new(TerminateAsync());

    // --------- DUPLEX START HANDLER --------------------------------
    private async Task OnExecutionRequest(ActivityExecutionRequested req) {
        if (req.ActivityId != _id) return;        // not mine
        if (_state != ActivityState.Idle) return; // already running

        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(req.CancellationToken);
        await RunSelfAsync(req.Input, req.ContextSnapshot, _linkedCts.Token);
    }

    // --------- PRIVATE RUN-SELF (NO INBOUND API) -------------------
    private async Task RunSelfAsync(object? input, TopicWorkflowContextV2 ctx, CancellationToken ct) {
        try {
            await TransitionToAsync(ActivityState.Running, ct);
            await PublishAsync(ActivityEventType.ActivityStarted,
                new { Input = input, ContextKeys = ctx.GetKeys() });

            // ---- DOMAIN WORK (EXTENSIBLE VIA OVERRIDE) ---------------
            await ExecuteCoreAsync(ctx, input, ct);

            await TransitionToAsync(ActivityState.Completed, ct);
            await PublishAsync(ActivityEventType.ActivityCompleted,
                new { ContextKeys = ctx.GetKeys() });
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            await TransitionToAsync(ActivityState.TimedOut, ct);
            await PublishAsync(ActivityEventType.ActivityTimedOut);
        } catch (Exception ex) {
            await TransitionToAsync(ActivityState.Failed, ct);
            await PublishAsync(ActivityEventType.ActivityFailed,
                new { Reason = ex.Message, ExceptionType = ex.GetType().Name });
        }
    }

    // --------- DOMAIN WORK (OVERRIDE IN DERIVED TYPES) -------------
    /// <summary>
    /// Override this method in derived activity types to implement custom logic.
    /// This is the extension point for all concrete activities.
    /// </summary>
    protected virtual async Task ExecuteCoreAsync(TopicWorkflowContextV2 ctx, object? input, CancellationToken ct) {
        // Default: no-op (derived types override)
        await Task.CompletedTask;
    }

    // --------- HELPERS ---------------------------------------------
    private async Task TransitionToAsync(ActivityState next, CancellationToken ct) {
        if (_state == next) return;
        var prev = _state;
        _state = next;
        _logger?.LogDebug("[{Id}] {Prev} -> {Next}", _id, prev, next);
        await PublishAsync(ActivityEventType.ActivityStateChanged,
            new { Previous = prev.ToString(), Next = next.ToString() });
    }

    /// <summary>
    /// Publishes an activity event to the internal bus.
    /// TopicFlowV2 will forward selected events to the domain bus.
    /// </summary>
    protected Task PublishAsync(string eventType, object? payload = null) =>
        _bus.PublishAsync(new ActivityEventEnvelope(
            SourceId: _id,
            EventType: eventType,
            Version: SCHEMA_VERSION,
            Timestamp: DateTime.UtcNow,
            Payload: payload), CancellationToken.None);

    // --------- EVENT TYPE CONSTANTS --------------------------------
    internal static class ActivityEventType {
        public const string ActivityStarted = "Activity.Started";
        public const string ActivityCompleted = "Activity.Completed";
        public const string ActivityFailed = "Activity.Failed";
        public const string ActivityTimedOut = "Activity.TimedOut";
        public const string ActivityTerminated = "Activity.Terminated";
        public const string ActivityStateChanged = "Activity.StateChanged";
        public const string ActivityWaiting = "Activity.Waiting";
        public const string ActivityResumed = "Activity.Resumed";
    }

    // --------- STATE ENUM ------------------------------------------
    private enum ActivityState { Idle, Running, Completed, Failed, TimedOut, Terminated }
}

// --------- INBOUND REQUEST EVENT (SAME ASSEMBLY) -----------------
/// <summary>
/// Event published by TopicFlowV2 to trigger activity execution.
/// Activities self-subscribe and filter by ActivityId.
/// </summary>
public record ActivityExecutionRequested(
    string ActivityId,
    object? Input,
    TopicWorkflowContextV2 ContextSnapshot,
    CancellationToken CancellationToken
);

// ------------------------------------------------------------------
//  ITerminable  –  unchanged, assembly-neutral
// ------------------------------------------------------------------
public interface ITerminable {
    bool IsTerminated { get; }
    Task TerminateAsync();
}

// ------------------------------------------------------------------
//  IEventBus  –  minimal, internal to TopicFlow assembly
// ------------------------------------------------------------------
public interface IEventBus {
    Task PublishAsync<T>(T envelope, CancellationToken ct = default);
    void Subscribe<T>(Func<T, Task> handler);
    void Unsubscribe<T>(Func<T, Task> handler);

    // raw stream for same-assembly consumers
    event Func<object, CancellationToken, Task>? OnPublish;
}

// ------------------------------------------------------------------
//  InMemoryEventBus  –  thread-safe implementation for V2
// ------------------------------------------------------------------
public sealed class InMemoryEventBus : IEventBus {
    // publish side
    public event Func<object, CancellationToken, Task>? OnPublish;

    // subscribe side (thread-safe)
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _handlers = new();

    // ----------  PUBLISH  ------------------------------------------
    async Task IEventBus.PublishAsync<T>(T envelope, CancellationToken ct) {
        // Raw event stream
        var handler = OnPublish;
        if (handler != null) await handler(envelope!, ct);

        // Typed subscribers
        if (_handlers.TryGetValue(typeof(T), out var bag)) {
            foreach (var d in bag) {
                await ((Func<T, Task>)d)(envelope);
            }
        }
    }

    // ----------  SUBSCRIBE / UNSUBSCRIBE  --------------------------
    public void Subscribe<T>(Func<T, Task> handler) {
        var key = typeof(T);
        var bag = _handlers.GetOrAdd(key, _ => new ConcurrentBag<Delegate>());
        bag.Add(handler);
    }

    public void Unsubscribe<T>(Func<T, Task> handler) {
        if (_handlers.TryGetValue(typeof(T), out var bag)) {
            // Note: ConcurrentBag doesn't support efficient removal
            // For production, consider ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Delegate>>
            // with subscription tokens. This is acceptable for V2 prototype.
        }
    }
}

/// <summary>
/// A key-value store for workflow state (V2 version).
/// Thread-safe for concurrent access by multiple activities.
/// </summary>
public class TopicWorkflowContextV2 {
    private readonly ConcurrentDictionary<string, object> _values = new();

    /// <summary>
    /// Sets a value in the workflow context.
    /// </summary>
    public void SetValue(string key, object? value) {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (value == null) {
            _values.TryRemove(key, out _);
            return;
        }

        _values[key] = value;
    }

    /// <summary>
    /// Gets a value from the workflow context.
    /// </summary>
    public T? GetValue<T>(string key) {
        if (string.IsNullOrEmpty(key) || !_values.TryGetValue(key, out var value))
            return default;

        if (value is T typedValue)
            return typedValue;

        try {
            // Handle JsonElement (common in JSON-based contexts)
            if (value is JsonElement jsonElement) {
                object? unwrapped = jsonElement.ValueKind switch {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => jsonElement.TryGetInt64(out var i64) ? i64 : jsonElement.TryGetDouble(out var dbl) ? dbl : (object?)null,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => jsonElement.ToString()
                };

                if (unwrapped is T direct)
                    return direct;

                if (unwrapped != null)
                    return (T)Convert.ChangeType(unwrapped, typeof(T));
            }

            // Fallback: normal conversion
            return (T)Convert.ChangeType(value, typeof(T));
        } catch {
            return default;
        }
    }

    /// <summary>
    /// Gets a value from the workflow context or a default value if not found.
    /// </summary>
    public T GetValue<T>(string key, T defaultValue) {
        var value = GetValue<T>(key);
        return value != null ? value : defaultValue;
    }

    /// <summary>
    /// Checks if the workflow context contains a specific key.
    /// </summary>
    public bool ContainsKey(string key) {
        return !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
    }

    /// <summary>
    /// Gets all keys in the workflow context.
    /// </summary>
    public IEnumerable<string> GetKeys() {
        return _values.Keys;
    }

    /// <summary>
    /// Clears all values from the workflow context.
    /// </summary>
    public void Clear() {
        _values.Clear();
    }

    /// <summary>
    /// Removes a specific key from the context.
    /// </summary>
    public void RemoveValue(string key) {
        if (string.IsNullOrEmpty(key)) return;
        _values.TryRemove(key, out _);
    }

    /// <summary>
    /// Gets the number of items in the workflow context.
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// Creates a shallow snapshot of the current context state.
    /// Used when passing context to activities in event-driven mode.
    /// </summary>
    public TopicWorkflowContextV2 Snapshot() {
        var copy = new TopicWorkflowContextV2();
        foreach (var kvp in _values) {
            copy._values[kvp.Key] = kvp.Value;
        }
        return copy;
    }

    /// <summary>
    /// Merges values from another context into this one.
    /// Used to collect activity results back into the topic context.
    /// </summary>
    public void MergeFrom(TopicWorkflowContextV2 other) {
        if (other == null) return;
        foreach (var kvp in other._values) {
            _values[kvp.Key] = kvp.Value;
        }
    }

    public override string ToString() {
        var sb = new System.Text.StringBuilder();
        foreach (var kvp in _values) {
            sb.Append(kvp.Key);
            sb.Append(": ");
            if (kvp.Value is string s)
                sb.Append(s);
            else if (kvp.Value != null)
                sb.Append(kvp.Value.GetType().ToString());
            else
                sb.Append("null");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
