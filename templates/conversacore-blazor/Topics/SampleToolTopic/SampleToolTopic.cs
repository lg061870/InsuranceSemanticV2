using ConversaCore.Authoring;
using ConversaCore.BlazorTemplateHost.Configuration;
using ConversaCore.BlazorTemplateHost.Tools;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Tools;
using Microsoft.Extensions.Logging;

namespace ConversaCore.BlazorTemplateHost.Topics.SampleToolTopic;

/// <summary>
/// Bounded generated-style tool sample demonstrating typed tool contracts,
/// topic allowlists (AllowedToolIds), explicit human confirmation enforcement,
/// and safe execution through InvokeToolActivity.
/// </summary>
public sealed class SampleToolTopic : ComposedTopicFlow
{
    public const string PromptActivityId = "sample.tool.welcome";
    public const string LookupCardActivityId = "sample.tool.lookup-card";
    public const string LookupModelContextKey = "sample.tool.lookup.request";
    public const string InvokeLookupActivityId = "sample.tool.invoke-lookup";
    public const string LookupResultContextKey = "sample.item.lookup.result";
    public const string ConfirmOrderActivityId = "sample.tool.confirm-order";
    public const string BranchConfirmId = "sample.tool.branch-confirm";
    public const string InvokeOrderActivityId = "sample.tool.invoke-order";
    public const string OrderResultContextKey = "sample.order.create.result";
    public const string BranchCancelId = "sample.tool.branch-cancel";
    public const string CancelActivityId = "sample.tool.cancel";
    public const string CompleteActivityId = "sample.tool.complete";

    private readonly ILogger<SampleToolTopic> _logger;
    private readonly IWorkflowActivityFactory _activities;
    private readonly IToolExecutor _toolExecutor;
    private readonly IConversationSession _session;
    private readonly IServiceProvider _services;

    public SampleToolTopic(
        TopicWorkflowContext context,
        ILogger<SampleToolTopic> logger,
        IWorkflowActivityFactory activities,
        IToolExecutor toolExecutor,
        IConversationSession session,
        IServiceProvider services)
        : base(context, logger, ConversaCoreTopicRegistration.SampleToolTopicId)
    {
        _logger = logger;
        _activities = activities ?? throw new ArgumentNullException(nameof(activities));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(message) &&
            (message.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("order", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("lookup", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(1.0f);
        }

        return Task.FromResult(0.0f);
    }

    protected override void ComposeWorkflow()
    {
        // 1. Welcome prompt introducing the tool capability
        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            PromptActivityId,
            systemPrompt: "You are an assistant demonstrating ConversaCore tool capabilities.",
            userPromptTemplate: "Welcome the user and explain that we can look up an item and place an order.")));

        // 2. Adaptive card collecting ItemId to look up
        Add(_activities.CreateAdaptiveCard<SampleLookupRequest>(new GeneratedAdaptiveCardDefinition(
            LookupCardActivityId,
            [
                new GeneratedAdaptiveCardFieldDefinition(
                    nameof(SampleLookupRequest.ItemId),
                    "Item ID (e.g. ITEM-101, ITEM-102)",
                    GeneratedAdaptiveCardInputKind.Text,
                    isRequired: true)
            ],
            title: "Lookup Item in Catalog",
            submitLabel: "Lookup Item",
            modelContextKey: LookupModelContextKey,
            customMessage: "Please specify the item ID to lookup",
            isRequired: true)));

        // 3. Read-Only Tool: Invoke SampleLookupTool with topic allowlist
        Add(new InvokeToolActivity<SampleLookupTool, SampleLookupRequest, SampleLookupResult>(
            InvokeLookupActivityId,
            SampleLookupTool.ToolId,
            _toolExecutor,
            requestFactory: ctx => ctx.GetValue<SampleLookupRequest>(LookupModelContextKey)
                ?? new SampleLookupRequest { ItemId = "ITEM-101" },
            executionContextFactory: _ => new ToolExecutionContext
            {
                ConversationId = _session.ConversationId,
                Subject = _session.Subject ?? "user",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _services,
                // Topic allowlist: only declared tools can be invoked in this topic
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    SampleLookupTool.ToolId
                },
                ConfirmationGranted = false,
                TrustedIdentityValidated = false
            },
            resultContextKey: LookupResultContextKey));

        // 4. Bounded QuickAnswer asking user to confirm order
        Add(_activities.CreateQuickAnswer(new QuickAnswerActivityDefinition(
            ConfirmOrderActivityId,
            "Would you like to place a sample order for this item?",
            ["Confirm Order", "Cancel"],
            isRequired: true)));

        // 5. Conditional Branching: Affirmative path invokes mutating tool with explicit confirmation
        Add(FlowConditionHelpers.IfCase(
            BranchConfirmId,
            ctx =>
            {
                var submission = ctx.GetValue<Dictionary<string, object>>(ConfirmOrderActivityId);
                return submission != null &&
                       submission.TryGetValue("answer", out var val) &&
                       string.Equals(val?.ToString(), "Confirm Order", StringComparison.OrdinalIgnoreCase);
            },
            new InvokeToolActivity<SampleOrderTool, SampleOrderRequest, SampleOrderResult>(
                InvokeOrderActivityId,
                SampleOrderTool.ToolId,
                _toolExecutor,
                requestFactory: ctx =>
                {
                    var lookup = ctx.GetValue<ToolResult<SampleLookupResult>>(LookupResultContextKey);
                    var itemId = lookup?.Value?.ItemId ??
                                 ctx.GetValue<SampleLookupRequest>(LookupModelContextKey)?.ItemId ??
                                 "ITEM-101";

                    return new SampleOrderRequest
                    {
                        ItemId = itemId,
                        Quantity = 1,
                        CustomerName = "Demo Customer"
                    };
                },
                executionContextFactory: _ => new ToolExecutionContext
                {
                    ConversationId = _session.ConversationId,
                    Subject = _session.Subject ?? "user",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Services = _services,
                    // Topic allowlist: strictly declare the mutating tool ID
                    AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        SampleOrderTool.ToolId
                    },
                    // Confirmation granted explicitly from user QuickAnswer selection, NEVER from model output
                    ConfirmationGranted = true,
                    TrustedIdentityValidated = true,
                    IdempotencyKey = Guid.NewGuid().ToString("N")
                },
                resultContextKey: OrderResultContextKey)));

        // 6. Conditional Branching: Cancel path emits fallback message without mutating state
        Add(FlowConditionHelpers.IfCase(
            BranchCancelId,
            ctx =>
            {
                var submission = ctx.GetValue<Dictionary<string, object>>(ConfirmOrderActivityId);
                return submission != null &&
                       submission.TryGetValue("answer", out var val) &&
                       !string.Equals(val?.ToString(), "Confirm Order", StringComparison.OrdinalIgnoreCase);
            },
            new FallbackActivity(
                CancelActivityId,
                "Order cancelled. No modifications were made to the catalog or orders.")));

        // 7. Completion activity summarizing the entire flow
        Add(new SimpleActivity(
            CompleteActivityId,
            (ctx, _) =>
            {
                var lookup = ctx.GetValue<ToolResult<SampleLookupResult>>(LookupResultContextKey);
                var order = ctx.GetValue<ToolResult<SampleOrderResult>>(OrderResultContextKey);

                string reply;
                if (order?.Succeeded == true && order.Value != null)
                {
                    reply = $"Success! Order placed with confirmation {order.Value.OrderId} for item '{order.Value.ItemId}'. Status: {order.Value.Status}.";
                }
                else if (lookup?.Succeeded == true && lookup.Value != null)
                {
                    reply = $"Inquiry completed for item '{lookup.Value.ItemId}' ({lookup.Value.Name}) at ${lookup.Value.Price}. No order was placed.";
                }
                else
                {
                    reply = "Tool sample workflow completed.";
                }

                _logger.LogInformation("[SampleToolTopic] Completed with reply: {Reply}", reply);
                return Task.FromResult<object?>(reply);
            }));
    }
}
