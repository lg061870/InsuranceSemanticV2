using ConversaCore.Authoring;
using ConversaCore.BlazorTemplateHost.Configuration;
using ConversaCore.BlazorTemplateHost.Contracts;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;

namespace ConversaCore.BlazorTemplateHost.Topics.SampleHostOutputTopic;

/// <summary>
/// Bounded generated-style sample topic demonstrating typed one-way host notifications
/// and correlated two-way host interactions without anonymous EventTriggerActivity payloads
/// or exposing internal workflow context to the host.
/// </summary>
public sealed class SampleHostOutputTopic : ComposedTopicFlow
{
    public const string PromptWelcomeActivityId = "sample.host.welcome";
    public const string NotifyStartedActivityId = "sample.host.notify-started";
    public const string PromptInteractionActivityId = "sample.host.prompt-interaction";
    public const string InvokeInteractionActivityId = "sample.host.invoke-decision";
    public const string InteractionResultContextKey = "sample.host.decision.result";
    public const string NotifyCompletedActivityId = "sample.host.notify-completed";
    public const string CompleteActivityId = "sample.host.complete";

    public const string HostNotificationEventName = "sample.host.notification";
    public const string HostInteractionName = "sample.host.decision";

    private readonly ILogger<SampleHostOutputTopic> _logger;
    private readonly IWorkflowActivityFactory _activities;
    private readonly IConversationOutputDispatcher _dispatcher;
    private readonly IHostInteractionCoordinator _coordinator;
    private readonly IConversationSession _session;

    public SampleHostOutputTopic(
        TopicWorkflowContext context,
        ILogger<SampleHostOutputTopic> logger,
        IWorkflowActivityFactory activities,
        IConversationOutputDispatcher dispatcher,
        IHostInteractionCoordinator coordinator,
        IConversationSession session)
        : base(context, logger, ConversaCoreTopicRegistration.SampleHostOutputTopicId)
    {
        _logger = logger;
        _activities = activities ?? throw new ArgumentNullException(nameof(activities));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(message) &&
            (message.Contains("host", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("notification", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("interaction", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("decision", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(1.0f);
        }

        return Task.FromResult(0.0f);
    }

    protected override void ComposeWorkflow()
    {
        // 1. Standard Prompt: Introduce the host output sample in standard chat transcript
        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            PromptWelcomeActivityId,
            systemPrompt: "You are an assistant demonstrating ConversaCore host output integration.",
            userPromptTemplate: "Welcome the user and explain that this topic will emit a typed host notification and request a decision from the host shell.")));

        // 2. One-way Host Notification: Dispatches an immutable typed notification to the host
        Add(new PublishHostNotificationActivity<SampleHostNotification>(
            NotifyStartedActivityId,
            HostNotificationEventName,
            version: 1,
            payloadFactory: _ => new SampleHostNotification(
                NotificationId: "NOTIF-001",
                Title: "Host Topic Initiated",
                Message: "The host output topic flow has started and emitted a typed one-way notification.",
                Severity: "Info",
                Timestamp: DateTimeOffset.UtcNow),
            _dispatcher,
            _session));

        // 3. Standard Prompt: Inform user in-chat that a decision is now requested from the host shell
        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            PromptInteractionActivityId,
            systemPrompt: "You are an assistant demonstrating ConversaCore host output integration.",
            userPromptTemplate: "Notify the user that an approval decision has been requested from the host application interface outside the chat window.")));

        // 4. Correlated Two-Way Host Interaction: Pauses at this awaitable boundary until host responds
        Add(new InvokeHostInteractionActivity<SampleHostInteractionRequest, SampleHostInteractionResponse>(
            InvokeInteractionActivityId,
            HostInteractionName,
            version: 1,
            timeout: TimeSpan.FromSeconds(30),
            _coordinator,
            requestFactory: _ => new SampleHostInteractionRequest(
                RequestId: "REQ-101",
                Prompt: "Please select an approval tier in the host application shell.",
                AvailableOptions: ["Standard Tier", "Priority Tier", "Decline"],
                CorrelationData: "SESSION-DEMO-DATA"),
            resultContextKey: InteractionResultContextKey));

        // 5. Completion SimpleActivity: Summarizes the received host decision in standard chat
        Add(new SimpleActivity(
            CompleteActivityId,
            (ctx, _) =>
            {
                var response = ctx.GetValue<SampleHostInteractionResponse>(InteractionResultContextKey);
                string reply = response != null
                    ? $"Host decision received: '{response.SelectedOption}' (Confirmed: {response.Confirmed}). Notes: {response.Comments ?? "None"}."
                    : "Host interaction completed without a recorded response.";

                _logger.LogInformation("[SampleHostOutputTopic] Completed with reply: {Reply}", reply);
                return Task.FromResult<object?>(reply);
            }));

        // 6. One-way Host Notification: Dispatches a final completion notification to the host
        Add(new PublishHostNotificationActivity<SampleHostNotification>(
            NotifyCompletedActivityId,
            HostNotificationEventName,
            version: 1,
            payloadFactory: ctx =>
            {
                var response = ctx.GetValue<SampleHostInteractionResponse>(InteractionResultContextKey);
                return new SampleHostNotification(
                    NotificationId: "NOTIF-002",
                    Title: "Host Topic Completed",
                    Message: $"Topic completed with decision: {response?.SelectedOption ?? "None"}.",
                    Severity: "Success",
                    Timestamp: DateTimeOffset.UtcNow);
            },
            _dispatcher,
            _session));
    }
}
