using ConversaCore.Authoring;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace ConversaCore.Tests.Authoring;

/// <summary>
/// Compile-contract fixture shaped like generated C#: concrete typed model, ordinary constructor
/// injection, post-construction composition, and public authoring contracts only.
/// </summary>
public sealed class GeneratedStyleContractTopic : ComposedTopicFlow
{
    private readonly IWorkflowActivityFactory _activities;

    public GeneratedStyleContractTopic(
        TopicWorkflowContext context,
        ILogger<GeneratedStyleContractTopic> logger,
        IWorkflowActivityFactory activities)
        : base(context, logger, "generated.start")
    {
        _activities = activities;
    }

    protected override void ComposeWorkflow()
    {
        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            "generated.prompt",
            systemPrompt: "Return a short deterministic welcome.",
            userPromptTemplate: "Welcome the user.")));
        Add(_activities.CreateQuickAnswer(new QuickAnswerActivityDefinition(
            "generated.confirm",
            "Continue to the generated profile?",
            ["Yes", "No"],
            isRequired: true)));
        Add(_activities.CreateAdaptiveCard<GeneratedProfileModel>(new GeneratedAdaptiveCardDefinition(
            "generated.profile",
            [
                new GeneratedAdaptiveCardFieldDefinition(
                    "FullName", "Full name", GeneratedAdaptiveCardInputKind.Text, isRequired: true),
                new GeneratedAdaptiveCardFieldDefinition(
                    "Plan", "Plan", GeneratedAdaptiveCardInputKind.Choice, isRequired: true, choices:
                    [
                        new GeneratedAdaptiveCardChoice("Basic", "basic"),
                        new GeneratedAdaptiveCardChoice("Plus", "plus")
                    ])
            ],
            title: "Generated profile",
            submitLabel: "Finish",
            modelContextKey: "generated.profile.model",
            customMessage: "Complete the generated profile",
            isRequired: true)));
        Add(new SimpleActivity("generated.complete", "Generated workflow complete."));
    }
}

public sealed class GeneratedProfileModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Plan { get; set; } = string.Empty;
}
