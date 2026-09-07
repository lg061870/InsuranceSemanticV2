namespace ConversaCore.Runtime;

/// <summary>Applies the framework's active-topic interruption and fallback policy to one message.</summary>
/// <remarks>This is scoped orchestration, not the final public <see cref="IConversationRuntime"/> facade.
/// It delegates topic selection to <see cref="ITopicRouter"/> and execution to <see cref="IWorkflowRunner"/>.</remarks>
public interface IConversationMessageCoordinator
{
    /// <summary>Routes and delivers a message, preserving a waiting original topic when interrupted.</summary>
    Task<ConversationMessageOutcome> ProcessAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>An immutable record of the routing decision and, when one ran, its execution outcome.</summary>
public sealed record ConversationMessageOutcome(TopicRoutingDecision Routing, WorkflowExecutionOutcome? Execution);
