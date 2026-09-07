using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>Executes one mutable topic activation at a time for a conversation.</summary>
/// <remarks>The runner owns an activated instance and its lifecycle boundary; it does not route,
/// choose a start topic, implement the public conversation facade, or subscribe to legacy activity
/// events. Subtopic calls, interruption, reset, cards, host interactions, and typed public output
/// are intentionally owned by CC-206 through CC-210 and WP3.</remarks>
public interface IWorkflowRunner
{
    /// <summary>Activates <paramref name="topic"/> and starts it without treating user input as a routing decision.</summary>
    Task<WorkflowExecutionOutcome> StartAsync(TopicDescriptor topic, CancellationToken cancellationToken = default);

    /// <summary>Activates <paramref name="topic"/> and delivers a routed user message to it.</summary>
    Task<WorkflowExecutionOutcome> ActivateAndDeliverAsync(TopicDescriptor topic, string message,
        CancellationToken cancellationToken = default);

    /// <summary>Delivers a message to the retained active activation.</summary>
    Task<WorkflowExecutionOutcome> DeliverToActiveAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>Gets whether this runner retains a mutable active activation.</summary>
    bool HasActiveExecution { get; }
}

/// <summary>The runner-owned execution state after a completed command.</summary>
public enum WorkflowExecutionState
{
    /// <summary>The topic finished and its mutable activation was released.</summary>
    Completed,
    /// <summary>The topic awaits the next user input and remains active.</summary>
    WaitingForInput,
    /// <summary>The topic awaits subtopic coordination and remains active; CC-206 owns that coordination.</summary>
    WaitingForSubtopic,
    /// <summary>The topic returned without handling its input; its activation was released.</summary>
    NotHandled
}

/// <summary>Immutable, context-free translation of a legacy topic result for runner dispatch.</summary>
/// <remarks>It deliberately contains no mutable workflow context, activity instance, raw event,
/// or outbox payload. WP3 replaces this stopgap with typed <c>ConversationOutput</c> records.</remarks>
public sealed record WorkflowExecutionOutcome(
    TopicDescriptor Topic,
    WorkflowExecutionState State,
    string? Response,
    string? AdaptiveCardJson,
    bool IsHandled,
    string? RequestedSubtopicId);

/// <summary>Awaitable runner-internal output boundary.</summary>
/// <remarks>This is not the public conversation subscription API. It exists so execution awaits
/// output handoff instead of recreating raw event chains; CC-300 through CC-302 define the public
/// typed output stream and its buffering/subscriber policy.</remarks>
public interface IWorkflowOutputDispatcher
{
    /// <summary>Handles an immutable outcome after the runner has committed its session transition.</summary>
    Task DispatchAsync(WorkflowExecutionOutcome outcome, CancellationToken cancellationToken = default);
}
