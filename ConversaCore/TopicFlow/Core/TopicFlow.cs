using Microsoft.Extensions.Logging;
using ConversaCore.Topics;         // for ITopic, TopicResult
using ConversaCore.Models;         // for TopicWorkflowContext, ActivityResult
using ConversaCore.StateMachine;
using ConversaCore.Events;   // for TopicStateMachine<TState>
using ConversaCore.Core;

namespace ConversaCore.TopicFlow;

/// <summary>
/// Represents a flow of TopicFlowActivity nodes executed in queue order.
/// Activities enqueue when added, execution moves forward automatically.
/// A finite state machine (FSM) governs lifecycle transitions.
/// </summary>
public abstract class TopicFlow : ITopic, ITerminable {
    // ================================
    // TERMINATION SUPPORT
    // ================================
    
    /// <summary>
    /// Flag indicating whether this topic has been terminated.
    /// </summary>
    private bool _isTerminated = false;
    
    /// <summary>
    /// Gets whether this topic has been terminated.
    /// </summary>
    public bool IsTerminated => _isTerminated;
    
    /// <summary>
    /// Terminates the topic and all its activities, releasing resources and unsubscribing from events.
    /// This implementation complies with the ITerminable interface.
    /// </summary>
    public virtual void Terminate()
    {
        if (_isTerminated) return; // Already terminated
        
        _logger.LogInformation("[TopicFlow] Terminating topic {Name} (CurrentState={State})", Name, State);
        
        // Cancel any ongoing operations if we're in a running or waiting state
        if (State == FlowState.Running || State == FlowState.WaitingForInput)
        {
            _logger.LogDebug("[TopicFlow] Canceling ongoing operations for topic {Name}", Name);
            _isRunning = false;
            
            try
            {
                // Force transition to a terminal state for proper cleanup
                _fsm.ForceState(FlowState.Failed, "Terminated while active");
                OnTopicLifecycleChanged(TopicLifecycleState.Failed, "Topic terminated");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TopicFlow] Exception during state transition in termination for topic {Name}", Name);
            }
        }
        
        // Terminate all activities - priority on the current activity if one exists
        if (_currentActivityId != null && _activities.TryGetValue(_currentActivityId, out var currentActivity))
        {
            if (currentActivity is ITerminable terminableCurrentActivity)
            {
                _logger.LogDebug("[TopicFlow] Terminating current activity {ActivityId}", currentActivity.Id);
                try
                {
                    terminableCurrentActivity.Terminate();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TopicFlow] Exception terminating current activity {ActivityId}", currentActivity.Id);
                }
            }
        }
        
        // Now terminate all other activities
        foreach (var activity in _activities.Values)
        {
            // Skip the current activity since we already terminated it
            if (activity.Id == _currentActivityId) continue;
            
            if (activity is ITerminable terminable)
            {
                _logger.LogDebug("[TopicFlow] Terminating activity {ActivityId}", activity.Id);
                try
                {
                    terminable.Terminate();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TopicFlow] Exception terminating activity {ActivityId}", activity.Id);
                }
            }
        }
        
        // Clear collections
        ClearActivities();
        
        // Unregister all event handlers
        TopicLifecycleChanged = null;
        ActivityCreated = null;
        ActivityCompleted = null;
        TopicInserted = null;
        ActivityLifecycleChanged = null;
        
        // Reset state machine and state flags
        try
        {
            _fsm.Reset(FlowState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TopicFlow] Exception resetting state machine for topic {Name}", Name);
        }
        
        _currentActivityId = null;
        _isRunning = false;
        
        // Mark as terminated
        _isTerminated = true;
        
        _logger.LogInformation("[TopicFlow] Topic {Name} terminated successfully", Name);
    }
    
    /// <summary>
    /// Asynchronously terminates the topic and all its activities, releasing resources and unsubscribing from events.
    /// This implementation handles proper cleanup of async operations.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to cancel the termination process.</param>
    public virtual async Task TerminateAsync(CancellationToken cancellationToken = default)
    {
        if (_isTerminated) return; // Already terminated
        
        _logger.LogInformation("[TopicFlow] Asynchronously terminating topic {Name}", Name);
        
        // First perform synchronous termination for most resources
        Terminate();
        
        // Handle async cleanup specifically for activities that might need it
        if (_activities.Count > 0)
        {
            var terminationTasks = new List<Task>();
            
            foreach (var activity in _activities.Values)
            {
                if (activity is ITerminable terminable && !terminable.IsTerminated)
                {
                    terminationTasks.Add(terminable.TerminateAsync(cancellationToken));
                }
            }
            
            if (terminationTasks.Count > 0)
            {
                _logger.LogDebug("[TopicFlow] Waiting for {Count} activities to terminate asynchronously", terminationTasks.Count);
                
                try
                {
                    // Use WhenAll with a timeout to prevent hanging
                    var completedTask = await Task.WhenAny(
                        Task.WhenAll(terminationTasks),
                        Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)
                    );
                    
                    if (completedTask.IsCompleted && !completedTask.IsCanceled && !completedTask.IsFaulted)
                    {
                        _logger.LogDebug("[TopicFlow] All activities terminated asynchronously");
                    }
                    else
                    {
                        _logger.LogWarning("[TopicFlow] Timed out waiting for activities to terminate asynchronously");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TopicFlow] Exception during async termination of activities");
                }
            }
        }
        
        _logger.LogInformation("[TopicFlow] Topic {Name} terminated asynchronously", Name);
    }
    
    /// <summary>
    /// Resets the topic flow. Clears all activities, resets FSM, and calls InitializeActivities to rebuild.
    /// Override InitializeActivities in derived classes to rebuild activity pipeline.
    /// </summary>
    public virtual void Reset()
    {
        _logger.LogInformation("[TopicFlow] Reset called for {Name} (CurrentState={State})", Name, State);
        
        // If terminated, we can't reset properly
        if (_isTerminated)
        {
            _logger.LogWarning("[TopicFlow] Cannot reset topic {Name} because it has been terminated. Create a new instance instead.", Name);
            return;
        }
        
        try
        {
            // Reset all activities to Idle before clearing
            foreach (var activity in _activities.Values)
            {
                _logger.LogInformation("[TopicFlow] Resetting activity {ActivityId} from state {CurrentState}", activity.Id, activity.CurrentState);
                try
                {
                    activity.Reset();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TopicFlow] Exception while resetting activity {ActivityId}", activity.Id);
                    // Continue with other activities even if one fails
                }
            }
            
            // Clear collections after resetting activities
            ClearActivities();
            
            // Reset FSM state - force it back to Idle regardless of current state
            try
            {
                _fsm.Reset(FlowState.Idle);
                _fsm.ClearTransitionHistory();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TopicFlow] Exception while resetting state machine for {Name}", Name);
                // Try to force a clean state even if the normal reset fails
                _fsm.ForceState(FlowState.Idle, "Forced reset after error");
            }
            
            // Reset runtime state
            _currentActivityId = null;
            _isRunning = false;
            
            _logger.LogInformation("[TopicFlow] Reset completed for {Name}, FSM state={CurrentState}", Name, _fsm.CurrentState);
            
            // Notify about the reset via lifecycle event
            OnTopicLifecycleChanged(TopicLifecycleState.Created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TopicFlow] Failed to reset topic {Name}", Name);
            throw;
        }
    }
    
    /// <summary>
    /// Clears all activities and the activity queue. For use in derived classes.
    /// </summary>
    protected void ClearActivities()
    {
        _activities.Clear();
        _activityQueue.Clear();
    }
    public enum FlowState {
        Idle,
        Starting,
        Running,
        WaitingForInput,
        Completed,
        Failed
    }

    private readonly Dictionary<string, TopicFlowActivity> _activities = new();
    private readonly Queue<string> _activityQueue = new();
    private readonly TopicWorkflowContext _context;
    private readonly ILogger _logger;
    private readonly TopicStateMachine<FlowState> _fsm;

    private string? _currentActivityId;
    private bool _isRunning;

    public event EventHandler<TopicLifecycleEventArgs>? TopicLifecycleChanged;
    public event EventHandler<ActivityCreatedEventArgs>? ActivityCreated;
    public event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;
    public event EventHandler<TopicInsertedEventArgs>? TopicInserted;
    public event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;

    public TopicFlow(TopicWorkflowContext context, ILogger logger, string name = "TopicFlow") {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Name = name;

        _fsm = new TopicStateMachine<FlowState>(FlowState.Idle);
        _fsm.ConfigureTransition(FlowState.Idle, FlowState.Starting);
        _fsm.ConfigureTransition(FlowState.Starting, FlowState.Running);
        _fsm.ConfigureTransition(FlowState.Running, FlowState.WaitingForInput);
        _fsm.ConfigureTransition(FlowState.Running, FlowState.Completed);
        _fsm.ConfigureTransition(FlowState.Running, FlowState.Failed);
        _fsm.ConfigureTransition(FlowState.WaitingForInput, FlowState.Running);

        OnTopicLifecycleChanged(TopicLifecycleState.Created);
    }

    public TopicWorkflowContext Context => _context;
    public virtual string Name { get; protected set; }
    public virtual int Priority { get; protected set; } = 0;
    public FlowState State => _fsm.CurrentState;

    // ================================
    // Add / Remove
    // ================================
    public TopicFlow Add(TopicFlowActivity activity) {
        if (activity == null) throw new ArgumentNullException(nameof(activity));
        if (_activities.ContainsKey(activity.Id))
            throw new InvalidOperationException($"Activity '{activity.Id}' already exists in this flow. Each activity Id must be unique.");
        if (_isTerminated)
            throw new InvalidOperationException($"Cannot add activity '{activity.Id}' to terminated topic '{Name}'.");
            
        _activities[activity.Id] = activity;
        _activityQueue.Enqueue(activity.Id);

        // Use weak event pattern to avoid potential memory leaks
        activity.ActivityLifecycleChanged += (s, e) => {
            // Check if the topic is still active before propagating events
            if (!_isTerminated)
                ActivityLifecycleChanged?.Invoke(this, e);
        };

        activity.ActivityCompleted += async (s, e) => {
            // Check if the topic is still active before handling completion
            if (!_isTerminated) {
                _logger.LogInformation("[TopicFlow] Activity {ActivityId} completed", e.ActivityId);
                if (State == FlowState.WaitingForInput)
                    await _fsm.TryTransitionAsync(FlowState.Running, "Input collected");
            }
        };

        _logger.LogInformation("Added activity {ActivityId} to flow queue", activity.Id);
        return this;
    }

    public TopicFlow AddRange(IEnumerable<TopicFlowActivity> activities) {
        if (activities == null) throw new ArgumentNullException(nameof(activities));
        
        foreach (var activity in activities) {
            Add(activity);
        }
        
        return this;
    }

    public bool RemoveActivity(string activityId) {
        if (string.IsNullOrWhiteSpace(activityId))
            throw new ArgumentNullException(nameof(activityId));
        var removed = _activities.Remove(activityId);
        if (removed)
            _logger.LogInformation("Removed activity {ActivityId}", activityId);
        return removed;
    }

    public IEnumerable<TopicFlowActivity> GetAllActivities() => _activities.Values;

    // ================================
    // Event helpers
    // ================================
    protected void OnTopicLifecycleChanged(TopicLifecycleState state, object? data = null) =>
        TopicLifecycleChanged?.Invoke(this, new TopicLifecycleEventArgs(Name, state, data));

    protected void OnActivityCreated(string activityId, object? content) =>
        ActivityCreated?.Invoke(this, new ActivityCreatedEventArgs(activityId, content, _context));

    protected void OnActivityCompleted(string activityId) =>
        ActivityCompleted?.Invoke(this, new ActivityCompletedEventArgs(activityId, _context));

    protected void OnTopicInserted(string topicName) =>
        TopicInserted?.Invoke(this, new TopicInsertedEventArgs(topicName, _context));

    // ================================
    // ITopic
    // ================================
    public virtual Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
        Task.FromResult(1.0f);

    public virtual async Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        if (State == FlowState.WaitingForInput)
            return await ResumeAsync(message, cancellationToken);
        else
            return await RunAsync(cancellationToken);
    }

    // ================================
    // Execution
    // ================================
    public virtual async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        if (_activityQueue.Count == 0)
            throw new InvalidOperationException("No activities in flow. Add activities before starting.");

        await _fsm.TryTransitionAsync(FlowState.Starting, "RunAsync invoked");
        OnTopicLifecycleChanged(TopicLifecycleState.Starting);

        _currentActivityId = _activityQueue.Peek();
        _isRunning = true;

        _logger.LogInformation("Flow starting at {ActivityId}", _currentActivityId);

        await _fsm.TryTransitionAsync(FlowState.Running, "Flow started");
        OnTopicLifecycleChanged(TopicLifecycleState.Running);

        return await StepAsync(null, cancellationToken);
    }

    public async Task<TopicResult> ResumeAsync(string message, CancellationToken cancellationToken = default) {
        if (State != FlowState.WaitingForInput)
            throw new InvalidOperationException($"Flow is not waiting for input (state: {State}).");

        await _fsm.TryTransitionAsync(FlowState.Running, "Resuming after input");
        OnTopicLifecycleChanged(TopicLifecycleState.Resuming);

        return await StepAsync(message, cancellationToken);
    }

    public virtual async Task<TopicResult> StepAsync(object? input, CancellationToken ct) {
        if (!_isRunning) {
            // This might happen after a reset, so let's handle it gracefully
            _logger.LogWarning("StepAsync called but _isRunning=false. Attempting to recover.");
            _isRunning = true;
        }

        if (State != FlowState.Running && State != FlowState.Starting && State != FlowState.Idle) {
            _logger.LogWarning("StepAsync invoked but flow state={State}. Attempting to transition to Running.", State);
            
            // Try to recover by transitioning to Running if we're in Completed state after reset
            if (State == FlowState.Completed) {
                _logger.LogInformation("[TopicFlow] Recovering flow from Completed state in StepAsync");
                
                // Directly force the state to Running using our new method
                // This bypasses transition rules that might prevent recovery
                _fsm.ForceState(FlowState.Running, "Forced recovery in StepAsync from Completed state");
                
                // Make sure we're truly running
                _isRunning = true;
                
                // Notify lifecycle event for better tracking
                OnTopicLifecycleChanged(TopicLifecycleState.Running);
                
                _logger.LogInformation("[TopicFlow] Successfully recovered flow from Completed state using ForceState");
            } else {
                return TopicResult.CreateResponse($"Cannot step flow while in state {State}.", _context, requiresInput: true);
            }
        }

        while (_activityQueue.Count > 0) {
            if (ct.IsCancellationRequested) {
                await _fsm.TryTransitionAsync(FlowState.Failed, "Cancelled");
                OnTopicLifecycleChanged(TopicLifecycleState.Failed, "Cancelled");
                return TopicResult.CreateResponse("Flow cancelled.", _context, requiresInput: true);
            }

            if (string.IsNullOrWhiteSpace(_currentActivityId) ||
                !_activities.TryGetValue(_currentActivityId, out var activity)) {
                _logger.LogError("Current activity '{ActivityId}' not found.", _currentActivityId ?? "<null>");
                await _fsm.TryTransitionAsync(FlowState.Failed, "Activity not found");
                OnTopicLifecycleChanged(TopicLifecycleState.Failed, "Activity not found");
                return TopicResult.CreateResponse("Error: activity not found.", _context, requiresInput: true);
            }

            // 🛑 Skip completed/failed activities before running
            if (activity.CurrentState == ActivityState.Completed ||
                activity.CurrentState == ActivityState.Failed) {
                _logger.LogDebug("Skipping finished activity {Id} ({State})", activity.Id, activity.CurrentState);
                _activityQueue.Dequeue();
                _currentActivityId = _activityQueue.Count > 0 ? _activityQueue.Peek() : null;
                continue;
            }

            _logger.LogInformation("Executing activity {ActivityId}", activity.Id);

            ActivityResult ar;
            try {
                ar = await activity.RunAsync(_context, input);
            } catch (Exception ex) {
                _logger.LogError(ex, "Activity {ActivityId} threw.", activity.Id);
                await _fsm.TryTransitionAsync(FlowState.Failed, ex.Message);
                OnTopicLifecycleChanged(TopicLifecycleState.Failed, ex);
                return TopicResult.CreateResponse($"Error: {ex.Message}", _context, requiresInput: true);
            }

            input = null;

            if (ar.ModelContext != null)
                OnActivityCreated(activity.Id, ar.ModelContext);

            if (!string.IsNullOrEmpty(ar.Message))
                OnActivityCreated(activity.Id, ar.Message);


            if (ar.IsWaiting) {
                await _fsm.TryTransitionAsync(FlowState.WaitingForInput, "Activity requested input");
                OnTopicLifecycleChanged(TopicLifecycleState.WaitingForUserInput, ar);
                return TopicResult.CreateResponse(ar.Message ?? "Waiting for user input…", _context, requiresInput: true);
            }

            // NEW: Handle sub-topic waiting - pause this topic and signal orchestrator
            if (ar.IsWaitingForSubTopic) {
                _logger.LogInformation("Activity {ActivityId} is waiting for sub-topic '{SubTopic}' to complete", activity.Id, ar.SubTopicName);
                
                // DON'T dequeue the activity - we need to resume from here
                // DON'T mark activity as completed - it's still waiting
                
                await _fsm.TryTransitionAsync(FlowState.WaitingForInput, "Activity waiting for sub-topic");
                OnTopicLifecycleChanged(TopicLifecycleState.WaitingForSubTopic, ar);
                
                // Return special result that signals orchestrator to start sub-topic
                return TopicResult.CreateSubTopicTrigger(ar.SubTopicName!, ar.Message, _context);
            }

            // NEW: Handle activity end - complete the topic immediately
            if (ar.IsEnd) {
                _logger.LogInformation("Activity {ActivityId} signaled end of topic", activity.Id);
                
                OnActivityCompleted(activity.Id);
                
                await _fsm.TryTransitionAsync(FlowState.Completed, "Activity signaled end");
                OnTopicLifecycleChanged(TopicLifecycleState.Completed, ar);
                return TopicResult.CreateCompleted(ar.Message ?? string.Empty, _context);
            }

            OnActivityCompleted(activity.Id);

            _activityQueue.Dequeue();
            _currentActivityId = _activityQueue.Count > 0 ? _activityQueue.Peek() : null;

            if (_currentActivityId == null) {
                await _fsm.TryTransitionAsync(FlowState.Completed, "Queue exhausted");
                OnTopicLifecycleChanged(TopicLifecycleState.Completed, ar);
                return TopicResult.CreateCompleted(string.Empty, _context);
            }

            _logger.LogInformation("Dequeued next activity: {ActivityId}", _currentActivityId);
        }

        await _fsm.TryTransitionAsync(FlowState.Completed, "Queue exhausted (safety)");
        OnTopicLifecycleChanged(TopicLifecycleState.Completed);
        return TopicResult.CreateCompleted(string.Empty, _context);
    }

    public TopicFlowActivity? GetCurrentActivity() {
        if (_currentActivityId != null && _activities.TryGetValue(_currentActivityId, out var act))
            return act;
        return null;
    }
}
