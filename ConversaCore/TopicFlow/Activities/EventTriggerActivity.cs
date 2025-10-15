using System;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow.Activities;

/// <summary>
/// Event arguments for custom events triggered by EventTriggerActivity.
/// </summary>
public class CustomEventTriggeredEventArgs : EventArgs {
    public string EventName { get; }
    public object? EventData { get; }
    public TopicWorkflowContext Context { get; }
    public bool WaitForResponse { get; }
    
    public CustomEventTriggeredEventArgs(string eventName, object? eventData, TopicWorkflowContext context, bool waitForResponse) {
        EventName = eventName;
        EventData = eventData;
        Context = context;
        WaitForResponse = waitForResponse;
    }
}

/// <summary>
/// Interface for activities that can trigger custom events.
/// </summary>
public interface ICustomEventTriggeredActivity {
    event EventHandler<CustomEventTriggeredEventArgs>? CustomEventTriggered;
}

/// <summary>
/// An activity that triggers custom events to communicate with the UI layer.
/// Supports both fire-and-forget events (continues execution immediately) 
/// and blocking events (waits for UI response before continuing).
/// </summary>
public class EventTriggerActivity : TopicFlowActivity, ICustomEventTriggeredActivity {
    private readonly ILogger? _logger;
    private readonly IConversationContext? _conversationContext;
    private readonly string _eventName;
    private readonly object? _eventData;
    private readonly bool _waitForResponse;
    private readonly string? _responseContextKey;

    /// <summary>
    /// Event that signals the orchestrator that a custom event should be triggered.
    /// </summary>
    public event EventHandler<CustomEventTriggeredEventArgs>? CustomEventTriggered;

    /// <summary>
    /// Override the allowed transitions to include WaitingForUserInput from Running state.
    /// </summary>
    protected override Dictionary<ActivityState, HashSet<ActivityState>> AllowedTransitions =>
        new()
        {
            { ActivityState.Idle,      new() { ActivityState.Created } },
            { ActivityState.Created,   new() { ActivityState.Running, ActivityState.Failed } },
            { ActivityState.Running,   new() { ActivityState.Completed, ActivityState.Failed, ActivityState.WaitingForUserInput } },
            { ActivityState.WaitingForUserInput, new() { ActivityState.Completed, ActivityState.Failed } },
            { ActivityState.Completed, new() { } },   // terminal
            { ActivityState.Failed,    new() { } }    // terminal
        };

    /// <summary>
    /// Creates a new EventTriggerActivity.
    /// </summary>
    /// <param name="id">Unique identifier for this activity</param>
    /// <param name="eventName">Name of the event to trigger</param>
    /// <param name="eventData">Optional data to include with the event</param>
    /// <param name="waitForResponse">If true, waits for UI response before continuing; if false, fires event and continues immediately</param>
    /// <param name="responseContextKey">If waiting for response, key to store the response data in context</param>
    /// <param name="logger">Optional logger</param>
    /// <param name="conversationContext">Optional conversation context</param>
    public EventTriggerActivity(
        string id,
        string eventName,
        object? eventData = null,
        bool waitForResponse = false,
        string? responseContextKey = null,
        ILogger? logger = null,
        IConversationContext? conversationContext = null)
        : base(id) {
        
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));
            
        if (waitForResponse && string.IsNullOrWhiteSpace(responseContextKey))
            throw new ArgumentException("Response context key is required when waiting for response", nameof(responseContextKey));

        _eventName = eventName;
        _eventData = eventData;
        _waitForResponse = waitForResponse;
        _responseContextKey = responseContextKey;
        _logger = logger;
        _conversationContext = conversationContext;
    }

    /// <summary>
    /// Factory method for fire-and-forget events.
    /// </summary>
    public static EventTriggerActivity CreateFireAndForget(
        string id,
        string eventName,
        object? eventData = null,
        ILogger? logger = null,
        IConversationContext? conversationContext = null) {
        
        return new EventTriggerActivity(id, eventName, eventData, false, null, logger, conversationContext);
    }

    /// <summary>
    /// Factory method for blocking events that wait for UI response.
    /// </summary>
    public static EventTriggerActivity CreateWaitForResponse(
        string id,
        string eventName,
        string responseContextKey,
        object? eventData = null,
        ILogger? logger = null,
        IConversationContext? conversationContext = null) {
        
        return new EventTriggerActivity(id, eventName, eventData, true, responseContextKey, logger, conversationContext);
    }

    protected override Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default) {

        TransitionTo(ActivityState.Running, input);

        try {
            _logger?.LogInformation("[EventTriggerActivity] Triggering custom event '{EventName}' (WaitForResponse: {WaitForResponse})", 
                _eventName, _waitForResponse);

            // Fire the custom event
            var eventArgs = new CustomEventTriggeredEventArgs(_eventName, _eventData, context, _waitForResponse);
            CustomEventTriggered?.Invoke(this, eventArgs);

            _logger?.LogInformation("[EventTriggerActivity] Custom event '{EventName}' triggered successfully", _eventName);

            if (_waitForResponse) {
                // Transition to waiting for input state
                TransitionTo(ActivityState.WaitingForUserInput, eventArgs);
                
                // Store information about what we're waiting for
                context.SetValue($"{Id}_WaitingForEvent", _eventName);
                context.SetValue($"{Id}_ResponseKey", _responseContextKey);
                
                _logger?.LogInformation("[EventTriggerActivity] Waiting for UI response for event '{EventName}'", _eventName);
                
                // Return a waiting result - the UI must trigger a resume with response data
                return Task.FromResult(ActivityResult.WaitForInput(
                    $"Waiting for UI response to event '{_eventName}'", 
                    eventArgs));
            } else {
                // Fire and forget - continue immediately
                TransitionTo(ActivityState.Completed, eventArgs);
                
                _logger?.LogInformation("[EventTriggerActivity] Fire-and-forget event '{EventName}' completed", _eventName);
                
                return Task.FromResult(ActivityResult.Continue(
                    $"Event '{_eventName}' triggered successfully", 
                    eventArgs));
            }
        } catch (Exception ex) {
            _logger?.LogError(ex, "[EventTriggerActivity] Failed to trigger event '{EventName}'", _eventName);
            TransitionTo(ActivityState.Failed, ex);
            throw;
        }
    }

    /// <summary>
    /// Handles response from the UI when in waiting mode.
    /// This should be called by the orchestrator when the UI provides a response.
    /// </summary>
    public void HandleUIResponse(TopicWorkflowContext context, object? responseData) {
        if (!_waitForResponse) {
            _logger?.LogWarning("[EventTriggerActivity] HandleUIResponse called but activity is not waiting for response");
            return;
        }

        if (CurrentState != ActivityState.WaitingForUserInput) {
            _logger?.LogWarning("[EventTriggerActivity] HandleUIResponse called but activity is not in waiting state (current: {State})", CurrentState);
            return;
        }

        try {
            _logger?.LogInformation("[EventTriggerActivity] Received UI response for event '{EventName}'", _eventName);

            // Store the response data in context if a key was provided
            if (!string.IsNullOrEmpty(_responseContextKey)) {
                context.SetValue(_responseContextKey, responseData);
                _logger?.LogDebug("[EventTriggerActivity] Stored response data in context key '{Key}'", _responseContextKey);
            }

            // Clean up waiting markers
            context.RemoveValue($"{Id}_WaitingForEvent");
            context.RemoveValue($"{Id}_ResponseKey");

            // Complete the activity
            TransitionTo(ActivityState.Completed, responseData);
            
            _logger?.LogInformation("[EventTriggerActivity] Successfully completed after receiving UI response");
        } catch (Exception ex) {
            _logger?.LogError(ex, "[EventTriggerActivity] Error handling UI response for event '{EventName}'", _eventName);
            TransitionTo(ActivityState.Failed, ex);
        }
    }

    /// <summary>
    /// Gets information about what this activity is waiting for (if applicable).
    /// </summary>
    public (string EventName, string? ResponseKey, bool IsWaiting) GetWaitingInfo() {
        return (_eventName, _responseContextKey, CurrentState == ActivityState.WaitingForUserInput);
    }

    public override string ToString() {
        var mode = _waitForResponse ? "WaitForResponse" : "FireAndForget";
        return $"EventTriggerActivity({Id}: {_eventName}, {mode})";
    }
}