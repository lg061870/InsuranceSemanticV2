using Microsoft.Extensions.Logging;
using ConversaCore.Topics;         // for ITopic, TopicResult
using ConversaCore.Models;         // for TopicWorkflowContext, ActivityResult
using ConversaCore.StateMachine;
using ConversaCore.Events;         // for TopicStateMachine<TState>

namespace ConversaCore.TopicFlow;

/// <summary>
/// Represents a flow of TopicFlowActivity nodes executed in queue order.
/// Activities enqueue when added, execution moves forward automatically.
/// A finite state machine (FSM) governs lifecycle transitions.
/// </summary>
public class TopicFlow : ITopic {
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

    // ======================================================
    // Reset / Clear
    // ======================================================
    public virtual void Reset() {
        ClearActivities();
    }

    protected void ClearActivities() {
        _activities.Clear();
        _activityQueue.Clear();
    }

    // ======================================================
    // Add / Remove Activities
    // ======================================================
    public TopicFlow Add(TopicFlowActivity activity) {
        if (activity == null)
            throw new ArgumentNullException(nameof(activity));

        if (_activities.ContainsKey(activity.Id))
            throw new InvalidOperationException($"Activity '{activity.Id}' already exists in this flow.");

        _activities[activity.Id] = activity;
        _activityQueue.Enqueue(activity.Id);

        // Event forwarding
        activity.ActivityLifecycleChanged += (s, e) => ActivityLifecycleChanged?.Invoke(this, e);

        activity.ActivityCompleted += async (s, e) => {
            _logger.LogInformation("[TopicFlow] Activity {ActivityId} completed", e.ActivityId);
            if (State == FlowState.WaitingForInput)
                await _fsm.TryTransitionAsync(FlowState.Running, "Input collected");
        };

        _logger.LogInformation("[TopicFlow] Added activity {ActivityId} to flow queue", activity.Id);
        return this;
    }

    /// <summary>
    /// Adds multiple activities to the flow in sequence.
    /// </summary>
    public TopicFlow AddRange(IEnumerable<TopicFlowActivity>? activities) {
        if (activities == null) {
            _logger.LogWarning("[TopicFlow] AddRange called with null activities");
            return this;
        }

        foreach (var activity in activities) {
            if (activity == null) {
                _logger.LogWarning("[TopicFlow] Skipping null activity in AddRange");
                continue;
            }

            try {
                Add(activity);
            } catch (Exception ex) {
                _logger.LogError(ex, "[TopicFlow] Failed to add activity {ActivityId}", activity.Id);
            }
        }

        return this;
    }

    public bool RemoveActivity(string activityId) {
        if (string.IsNullOrWhiteSpace(activityId))
            throw new ArgumentNullException(nameof(activityId));

        var removed = _activities.Remove(activityId);
        if (removed)
            _logger.LogInformation("[TopicFlow] Removed activity {ActivityId}", activityId);

        return removed;
    }

    public IEnumerable<TopicFlowActivity> GetAllActivities() => _activities.Values;

    // ======================================================
    // Event helpers
    // ======================================================
    protected void OnTopicLifecycleChanged(TopicLifecycleState state, object? data = null) =>
        TopicLifecycleChanged?.Invoke(this, new TopicLifecycleEventArgs(Name, state, data));

    protected void OnActivityCreated(string activityId, object? content) =>
        ActivityCreated?.Invoke(this, new ActivityCreatedEventArgs(activityId, content, _context));

    protected void OnActivityCompleted(string activityId) =>
        ActivityCompleted?.Invoke(this, new ActivityCompletedEventArgs(activityId, _context));

    protected void OnTopicInserted(string topicName) =>
        TopicInserted?.Invoke(this, new TopicInsertedEventArgs(topicName, _context));

    // ======================================================
    // ITopic Implementation
    // ======================================================
    public virtual Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
        Task.FromResult(1.0f);

    public virtual async Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        if (State == FlowState.WaitingForInput)
            return await ResumeAsync(message, cancellationToken);
        else
            return await RunAsync(cancellationToken);
    }

    // ======================================================
    // Execution
    // ======================================================
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
        if (!_isRunning)
            throw new InvalidOperationException("Flow is not running.");

        if (State != FlowState.Running && State != FlowState.Starting) {
            _logger.LogWarning("StepAsync invoked but flow state={State}.", State);
            return TopicResult.CreateResponse($"Cannot step flow while in state {State}.", _context, requiresInput: true);
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

            if (ar.IsWaitingForSubTopic) {
                _logger.LogInformation("Activity {ActivityId} is waiting for sub-topic '{SubTopic}'", activity.Id, ar.SubTopicName);
                await _fsm.TryTransitionAsync(FlowState.WaitingForInput, "Activity waiting for sub-topic");
                OnTopicLifecycleChanged(TopicLifecycleState.WaitingForSubTopic, ar);
                return TopicResult.CreateSubTopicTrigger(ar.SubTopicName!, ar.Message, _context);
            }

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
