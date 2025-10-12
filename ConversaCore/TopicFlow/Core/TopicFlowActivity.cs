using System.Text.Json;
using ConversaCore.Core;
using ConversaCore.Events;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow;

/// <summary>
/// Base class for all workflow activities with lifecycle state machine support.
/// Provides only the core/common transitions. Subtypes must override to expand.
/// </summary>
public abstract class TopicFlowActivity : ITerminable {
    // ================================
    // EVENTS
    // ================================
    public event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;
    public event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;

        /// <summary>
        /// Resets the activity state to Idle. Override in subclasses if needed.
        /// </summary>
        public virtual void Reset()
        {
            CurrentState = ActivityState.Idle;
        }
        
        // ================================
        // TERMINATION SUPPORT
        // ================================
        
        /// <summary>
        /// Flag indicating whether this activity has been terminated.
        /// </summary>
        private bool _isTerminated = false;
        
        /// <summary>
        /// Gets whether this activity has been terminated.
        /// </summary>
        public bool IsTerminated => _isTerminated;
        
        /// <summary>
        /// Optional cancellation token source for activity cancellation.
        /// </summary>
        protected CancellationTokenSource? _cancellationTokenSource;
        
        /// <summary>
        /// Terminates the activity, releasing resources and unsubscribing from events.
        /// This method implements proper cleanup to prevent memory leaks from event handlers
        /// and ongoing tasks.
        /// </summary>
        public virtual void Terminate()
        {
            if (_isTerminated) return; // Prevent multiple terminations
            
            _logger?.LogDebug("[{ActivityId}] Terminating activity of type {ActivityType}", Id, GetType().Name);
            
            // Cancel any ongoing operations
            try {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
            catch (Exception ex) {
                _logger?.LogWarning(ex, "[{ActivityId}] Exception during cancellation token cleanup", Id);
            }
            
            // Special handling for activities in waiting states
            if (CurrentState == ActivityState.WaitingForUserInput || CurrentState == ActivityState.Rendered) {
                _logger?.LogDebug("[{ActivityId}] Activity was waiting for input, force transitioning to Failed state", Id);
                try {
                    // Force transition to a terminal state to ensure proper cleanup
                    TransitionTo(ActivityState.Failed, "Terminated while waiting for input");
                }
                catch (Exception ex) {
                    _logger?.LogWarning(ex, "[{ActivityId}] Exception during forced state transition", Id);
                }
            }
            
            // Unregister event handlers - do this explicitly to avoid memory leaks
            ActivityLifecycleChanged = null;
            ActivityCompleted = null;
            
            // Clean up any model type references that could hold strong references
            ModelType = null;
            CustomDeserializer = null;
            
            // Reset state variables
            CurrentState = ActivityState.Idle;
            
            // Mark as terminated
            _isTerminated = true;
            
            _logger?.LogDebug("[{ActivityId}] Activity terminated successfully", Id);
        }
        
        /// <summary>
        /// Asynchronously terminates the activity, releasing resources and unsubscribing from events.
        /// This implementation handles activities that may have async cleanup requirements.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the termination process.</param>
        public virtual async Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            try {
                // First perform synchronous termination
                Terminate();
                
                // Special handling for derived classes that might have async cleanup requirements
                // This is a hook for subclasses to override with their own async cleanup logic
                await PerformAsyncCleanupAsync(cancellationToken);
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "[{ActivityId}] Exception during async termination", Id);
            }
        }
        
        /// <summary>
        /// Protected method for derived classes to override with their own async cleanup logic.
        /// Base implementation does nothing.
        /// </summary>
        protected virtual Task PerformAsyncCleanupAsync(CancellationToken cancellationToken = default)
        {
            // Default implementation does nothing
            return Task.CompletedTask;
        }

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
            // Ensure we don't pass null to ActivityCompletedEventArgs constructor
            ActivityCompleted?.Invoke(this, new ActivityCompletedEventArgs(Id, data ?? new object()));
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
