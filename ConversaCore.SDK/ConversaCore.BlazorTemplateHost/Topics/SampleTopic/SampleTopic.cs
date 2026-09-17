using ConversaCore.Authoring;
using ConversaCore.BlazorTemplateHost.Configuration;
using ConversaCore.BlazorTemplateHost.Topics.SampleTopic.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core;
using Microsoft.Extensions.Logging;

namespace ConversaCore.BlazorTemplateHost.Topics.SampleTopic;

/// <summary>
/// Minimal bounded generated-style sample topic demonstrating ComposedTopicFlow,
/// explicit constructor injection, IWorkflowActivityFactory, immutable Prompt/QuickAnswer
/// definitions, typed state via adaptive cards, completion, and fallback behavior.
/// </summary>
public partial class SampleTopic : ComposedTopicFlow
{
    public const string PromptActivityId = "sample.welcome";
    public const string ConfirmActivityId = "sample.confirm";
    public const string InputCardActivityId = "sample.input";
    public const string InputModelContextKey = "sample.input.model";
    public const string CompleteActivityId = "sample.complete";
    public const string FallbackActivityId = "sample.fallback";

    private readonly ILogger<SampleTopic> _logger;
    private readonly IWorkflowActivityFactory _activities;

    public SampleTopic(
        TopicWorkflowContext context,
        ILogger<SampleTopic> logger,
        IWorkflowActivityFactory activities)
        : base(context, logger, ConversaCoreTopicRegistration.SampleTopicId)
    {
        _logger = logger;
        _activities = activities ?? throw new ArgumentNullException(nameof(activities));
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            message.Contains("sample topic", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("sample", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(1.0f);
        }

        return Task.FromResult(0.0f);
    }

    protected override void ComposeWorkflow()
    {
        // 1. Welcome prompt via IWorkflowActivityFactory
        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            PromptActivityId,
            systemPrompt: "You are a friendly assistant for the ConversaCore template.",
            userPromptTemplate: "Provide a brief welcome introducing this conversational sample.")));

        // 2. Bounded QuickAnswer asking if the user wants to proceed
        Add(_activities.CreateQuickAnswer(new QuickAnswerActivityDefinition(
            ConfirmActivityId,
            "Would you like to explore the sample topic features?",
            ["Yes", "No"],
            isRequired: true)));

        // 3. Conditional Branching: Affirmative path collects typed card inputs
        Add(FlowConditionHelpers.IfCase(
            "sample.branch-yes",
            ctx =>
            {
                var submission = ctx.GetValue<Dictionary<string, object>>(ConfirmActivityId);
                return submission != null &&
                       submission.TryGetValue("answer", out var val) &&
                       string.Equals(val?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase);
            },
            _activities.CreateAdaptiveCard<SampleInputModel>(new GeneratedAdaptiveCardDefinition(
                InputCardActivityId,
                [
                    new GeneratedAdaptiveCardFieldDefinition(
                        nameof(SampleInputModel.Question),
                        "Question or topic of interest",
                        GeneratedAdaptiveCardInputKind.Text,
                        isRequired: true)
                ],
                title: "Explore Sample Topic",
                submitLabel: "Submit",
                modelContextKey: InputModelContextKey,
                customMessage: "Please provide your question or topic of interest",
                isRequired: true))));

        // 4. Conditional Branching: Fallback path if user declines or unhandled choice
        Add(FlowConditionHelpers.IfCase(
            "sample.branch-fallback",
            ctx =>
            {
                var submission = ctx.GetValue<Dictionary<string, object>>(ConfirmActivityId);
                return submission != null &&
                       submission.TryGetValue("answer", out var val) &&
                       !string.Equals(val?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase);
            },
            new FallbackActivity(
                FallbackActivityId,
                "No problem! You can explore the sample topic whenever you are ready.")));

        // 5. Completion activity verifying typed state or fallback completion
        Add(new SimpleActivity(
            CompleteActivityId,
            (ctx, _) =>
            {
                var model = ctx.GetValue<SampleInputModel>(InputModelContextKey);
                var reply = model != null && !string.IsNullOrWhiteSpace(model.Question)
                    ? $"Thank you. Received question: '{model.Question}'. Sample topic completed successfully."
                    : "Sample topic finished.";

                _logger.LogInformation("[SampleTopic] Completed with reply: {Reply}", reply);
                return Task.FromResult<object?>(reply);
            }));
    }
}

