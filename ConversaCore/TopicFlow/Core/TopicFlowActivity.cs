using System.Text.Json;
using ConversaCore.Events;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow;

/// <summary>
/// Base class for all workflow activities with lifecycle state machine support.
/// Provides only the core/common transitions. Subtypes must override to expand.
/// </summary>
public abstract class TopicFlowActivity {
    // ================================
    // EVENTS
    // ================================
    public event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;
    public event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;

    // ================================
    // FSM (STATE + TRANSITIONS)
    // ================================
    public ActivityState CurrentState { get; private set; } = ActivityState.Idle;

    protected virtual Dictionary<ActivityState, HashSet<ActivityState>> AllowedTransitions =>
        new()
        {
            { ActivityState.Idle,      new() { ActivityState.Created } },
            { ActivityState.Created,   new() { ActivityState.Running, ActivityState.Failed } },
            { ActivityState.Running,   new() { ActivityState.Completed, ActivityState.Failed } },
            { ActivityState.Completed, new() { } },   // terminal
            { ActivityState.Failed,    new() { } }    // terminal
        };

    protected void TransitionTo(ActivityState newState, object? data = null) {
        if (CurrentState == newState) return;

        if (!AllowedTransitions.TryGetValue(CurrentState, out var allowed) ||
            !allowed.Contains(newState)) {
            throw new InvalidOperationException(
                $"Invalid transition: {CurrentState} → {newState} (Activity={Id})");
        }

        var previous = CurrentState;
        CurrentState = newState;

        _logger?.LogDebug("[{ActivityId}] Transition {From} → {To} | Data={Data}",
            Id, previous, newState, data);

        ActivityLifecycleChanged?.Invoke(this,
            new ActivityLifecycleEventArgs(Id, newState, data));

        OnStateTransition(previous, newState, data);

        if (newState == ActivityState.Completed) {
            OnCompleted(data);
            ActivityCompleted?.Invoke(this, new ActivityCompletedEventArgs(Id, data));
        }
    }

    // ================================
    // EVENT HANDLER HOOKS
    // ================================
    protected virtual void OnStateTransition(ActivityState from, ActivityState to, object? data) { }
    protected virtual void OnCompleted(object? data) { }

    // ================================
    // OTHER CODE (PROPERTIES, CTOR, EXECUTION)
    // ================================
    public string Id { get; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public object? Metadata { get; set; }

    public Type? ModelType { get; set; }
    public string? ModelContextKey { get; set; }
    public string? SubmissionContextKey { get; set; }
    public Func<object, object?>? CustomDeserializer { get; set; }

    private readonly ILogger<TopicFlowActivity>? _logger;

    protected TopicFlowActivity(string id, ILogger<TopicFlowActivity>? logger = null) {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentNullException(nameof(id));

        Id = id;
        SubmissionContextKey = id;
        _logger = logger;

        TransitionTo(ActivityState.Created);
    }

    public virtual async Task<ActivityResult> RunAsync(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default) {
        TransitionTo(ActivityState.Running, input);

        try {
            if (input != null && !string.IsNullOrEmpty(SubmissionContextKey)) {
                context.SetValue(SubmissionContextKey, input);

                if (ModelType != null && !string.IsNullOrEmpty(ModelContextKey)) {
                    try {
                        object? model;
                        if (CustomDeserializer != null) {
                            model = CustomDeserializer(input);
                        }
                        else {
                            var json = input?.ToString() ?? "{}";
                            model = JsonSerializer.Deserialize(json, ModelType,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }
                        context.SetValue(ModelContextKey, model);
                    } catch (Exception ex) {
                        _logger?.LogWarning(ex,
                            "[{ActivityId}] Failed to deserialize input into {ModelType}", Id, ModelType);
                    }
                }
            }

            var result = await RunActivity(context, input, cancellationToken);

            if (result?.ModelContext != null && !string.IsNullOrEmpty(ModelContextKey)) {
                context.SetValue(ModelContextKey, result.ModelContext);
            }

            if (result != null && result.IsWaiting) {
                _logger?.LogInformation("[{ActivityId}] Activity waiting for input", Id);
            }
            else {
                TransitionTo(ActivityState.Completed, result);
            }

            return result!;
        } catch (Exception ex) {
            TransitionTo(ActivityState.Failed, ex);
            _logger?.LogError(ex, "[{ActivityId}] Activity failed", Id);
            throw;
        }
    }

    protected abstract Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Common lifecycle states. Only a subset is valid per subtype.
/// </summary>
public enum ActivityState {
    Idle,
    Created,
    Running,
    Completed,
    Failed,

    // Extended by subclasses
    Rendered,
    WaitingForUserInput,
    InputCollected,
    ValidationFailed,
    Triggered,
    Finalizing
}
