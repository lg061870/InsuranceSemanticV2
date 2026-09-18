using ConversaCore.Events;

namespace ConversaCore.Interfaces;

/// <summary>
/// Interface for activities that can trigger custom events.
/// </summary>
/// <remarks>
/// Deprecated in favor of typed host output envelopes: use
/// <see cref="ConversaCore.TopicFlow.PublishHostNotificationActivity{TPayload}"/> for one-way notifications or
/// <see cref="ConversaCore.TopicFlow.InvokeHostInteractionActivity{TRequest, TResponse}"/> for correlated two-way interactions.
/// </remarks>
[Obsolete("ICustomEventTriggeredActivity is deprecated and superseded by typed host outputs. Use PublishHostNotificationActivity<TPayload> or InvokeHostInteractionActivity<TRequest, TResponse> instead.")]
public interface ICustomEventTriggeredActivity {
    event EventHandler<CustomEventTriggeredEventArgs>? CustomEventTriggered;
}
