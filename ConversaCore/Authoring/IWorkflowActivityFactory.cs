using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

namespace ConversaCore.Authoring;

/// <summary>
/// Creates fresh workflow activities from bounded definitions using collaborators from the
/// current conversation scope. It is an explicit authoring dependency, not a service locator.
/// </summary>
public interface IWorkflowActivityFactory
{
    PromptActivity CreatePrompt(PromptActivityDefinition definition);
    QuickAnswerActivity CreateQuickAnswer(QuickAnswerActivityDefinition definition);
}
