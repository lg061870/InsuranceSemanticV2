# ConversaCore AI & Domain Developer Guide

This document is the official instruction manual for domain developers and AI coding assistants (e.g. Cursor, GitHub Copilot, ChatGPT, Antigravity, Claude). It provides the exact conventions, patterns, and safety guardrails for authoring ConversaCore topics and tools.

---

## 1. Core Architecture Primer (The 30-Second Mental Model)

ConversaCore separates conversation concerns into four clean layers:
1. **Topics (`ComposedTopicFlow`)**: The conversational flowchart. Coordinates dialogues, forms, branches, and tool calls.
2. **Tools (`IConversaTool<TRequest, TResult>`)**: The real-world actions (querying databases, calling REST APIs). Fully decoupled from the UI.
3. **Models**: Strongly typed records with DataAnnotations (`[Required]`, `[Range]`) for cards and tool requests/results.
4. **Runtime & UI (`IConversationRuntime`, `ConversaCore.UI`)**: Manages state, streaming, security validation, and chat window presentation.

```text
User / Chat UI
      │
      ▼
Topic Flow (ComposedTopicFlow)
      │
      ├─► Prompts & Adaptive Cards (IWorkflowActivityFactory)
      │
      └─► Tools via InvokeToolActivity (Validated by ToolExecutor)
            │
            ├── Topic Allowlist (AllowedToolIds)
            └── Human Confirmation (ConfirmationGranted from User Click)
```

---

## 2. Hard Rules & Safety Guardrails (AI Assistants: NEVER Violate These)

1. **Explicit Constructor Injection Only**:
   - Derive all new topics from `ComposedTopicFlow`.
   - Never use service locator (`GetService(...)` inside methods). Declare all dependencies in the topic constructor.
   - Never perform background or async work inside constructors.
2. **Deterministic Tool Invocations**:
   - Always invoke tools using `new InvokeToolActivity<TTool, TRequest, TResult>(...)`.
   - Always specify a topic allowlist: `AllowedToolIds = new HashSet<string> { "tool.id" }`.
3. **Strict Human Confirmation for Mutations**:
   - Mutating tools (`ToolSideEffect.Mutating`) with `ToolConfirmationPolicy { Required = true }` require `ConfirmationGranted = true`.
   - **CRITICAL**: `ConfirmationGranted` must be derived **strictly** from a deterministic user action (a card submission or QuickAnswer button click like "Confirm"). **Never allow LLM text or model output to grant confirmation.**
4. **Clean Reset Lifecycle**:
   - Topics must reset cleanly via `ComposedTopicFlow` without needing constructor rebuilds.
5. **Typed Host Boundary Only**:
   - Never use legacy untyped `EventTriggerActivity`.
   - Always use `PublishHostNotificationActivity<TPayload>` for one-way host notifications and `InvokeHostInteractionActivity<TRequest, TResponse>` for correlated host interactions.
   - Never expose `TopicWorkflowContext` to the host application.

---

## 3. Recipe 1: How to Author a Tool

Tools implement `IConversaTool<TRequest, TResult>`. Define typed request and result models:

```csharp
using System.ComponentModel.DataAnnotations;
using ConversaCore.Tools;

namespace MyDomainAgent.Tools;

// 1. Typed Request with Validation Attributes
public sealed record CheckInventoryRequest
{
    [Required]
    public string PartNumber { get; set; } = string.Empty;
}

// 2. Typed Result
public sealed record CheckInventoryResult(string PartNumber, int QuantityInStock, decimal UnitPrice);

// 3. Tool Implementation
public sealed class CheckInventoryTool : IConversaTool<CheckInventoryRequest, CheckInventoryResult>
{
    public const string ToolId = "parts.inventory.check";

    public static readonly ToolDescriptor Descriptor = new(
        toolId: ToolId,
        version: "1",
        displayName: "Check Inventory",
        description: "Looks up available stock for a specific part number.",
        requestType: typeof(CheckInventoryRequest),
        resultType: typeof(CheckInventoryResult),
        sideEffect: ToolSideEffect.ReadOnly, // or ToolSideEffect.Mutating
        confirmation: new ToolConfirmationPolicy { Required = false },
        reliability: new ToolReliabilityPolicy { Timeout = TimeSpan.FromSeconds(5) },
        implementationType: typeof(CheckInventoryTool));

    ToolDescriptor IConversaTool<CheckInventoryRequest, CheckInventoryResult>.Descriptor => Descriptor;

    public async ValueTask<ToolResult<CheckInventoryResult>> ExecuteAsync(
        CheckInventoryRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 👉 Domain developer inserts their API or database lookup here:
        var inStock = 42; 
        var price = 19.99m;

        return ToolResult<CheckInventoryResult>.Success(
            new CheckInventoryResult(request.PartNumber, inStock, price));
    }
}
```

---

## 4. Recipe 2: How to Author a Topic

Topics inherit from `ComposedTopicFlow` and compose their activity graph in `ComposeWorkflow()`:

```csharp
using ConversaCore.Authoring;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Tools;
using Microsoft.Extensions.Logging;
using MyDomainAgent.Tools;

namespace MyDomainAgent.Topics;

public sealed class CheckPartsTopic : ComposedTopicFlow
{
    private readonly IWorkflowActivityFactory _activities;
    private readonly IToolExecutor _toolExecutor;
    private readonly IConversationSession _session;
    private readonly IServiceProvider _services;

    // Explicit constructor injection
    public CheckPartsTopic(
        TopicWorkflowContext context,
        ILogger<CheckPartsTopic> logger,
        IWorkflowActivityFactory activities,
        IToolExecutor toolExecutor,
        IConversationSession session,
        IServiceProvider services)
        : base(context, logger, "parts.check")
    {
        _activities = activities;
        _toolExecutor = toolExecutor;
        _session = session;
        _services = services;
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(message.Contains("part", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.0f);
    }

    protected override void ComposeWorkflow()
    {
        // 1. Prompt / Card collecting Part Number
        Add(_activities.CreateAdaptiveCard<CheckInventoryRequest>(new GeneratedAdaptiveCardDefinition(
            "parts.card",
            [
                new GeneratedAdaptiveCardFieldDefinition(
                    nameof(CheckInventoryRequest.PartNumber), "Part Number", GeneratedAdaptiveCardInputKind.Text, isRequired: true)
            ],
            title: "Check Part Stock",
            submitLabel: "Check Stock",
            modelContextKey: "parts.request",
            isRequired: true)));

        // 2. Invoke Tool Activity with Allowlist
        Add(new InvokeToolActivity<CheckInventoryTool, CheckInventoryRequest, CheckInventoryResult>(
            "parts.invoke-check",
            CheckInventoryTool.ToolId,
            _toolExecutor,
            requestFactory: ctx => ctx.GetValue<CheckInventoryRequest>("parts.request")!,
            executionContextFactory: _ => new ToolExecutionContext
            {
                ConversationId = _session.ConversationId,
                Subject = _session.Subject ?? "user",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _services,
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CheckInventoryTool.ToolId }
            },
            resultContextKey: "parts.result"));

        // 3. Complete Activity
        Add(new SimpleActivity("parts.complete", (ctx, _) =>
        {
            var result = ctx.GetValue<ToolResult<CheckInventoryResult>>("parts.result");
            return Task.FromResult<object?>($"Stock for {result?.Value?.PartNumber}: {result?.Value?.QuantityInStock} units available.");
        }));
    }
}
```

---

## 5. Recipe 3: Registering in DI

In `Configuration/ConversaCoreTopicRegistration.cs`:

```csharp
// 1. Register Topic
builder.AddTopic<CheckPartsTopic>("parts.check", options =>
{
    options.DisplayName = "Check Parts";
    options.TriggerPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "check parts", "inventory" };
});

// 2. Register Tool
builder.AddTool<CheckInventoryTool>(CheckInventoryTool.Descriptor);
```

---

## 6. Recipe 4: How to Author Typed Host Notifications and Interactions

When the conversation needs to interact with the host application shell outside of the standard chat transcript (such as updating a dashboard widget, driving page navigation, or requesting user authorization in the containing app), use typed host outputs:

### 1. Define Typed Contracts (in `Contracts/`)
```csharp
namespace MyDomainAgent.Contracts;

// One-way notification
public sealed record InventoryAlertNotification(string PartNumber, int StockLevel, string Message);

// Correlated interaction request & response
public sealed record ManagerApprovalRequest(string RequestId, string PartNumber, decimal TotalCost);
public sealed record ManagerApprovalResponse(bool Approved, string ApproverName, string? Reason);
```

### 2. Topic Activities
In your `ComposedTopicFlow`:
```csharp
// One-way notification:
Add(new PublishHostNotificationActivity<InventoryAlertNotification>(
    "parts.notify-low-stock",
    "parts.low-stock",
    version: 1,
    ctx => new InventoryAlertNotification("PART-99", 2, "Low stock warning"),
    _dispatcher,
    _session));

// Correlated two-way interaction (topic awaits host response):
Add(new InvokeHostInteractionActivity<ManagerApprovalRequest, ManagerApprovalResponse>(
    "parts.request-approval",
    "parts.manager-approval",
    version: 1,
    timeout: TimeSpan.FromSeconds(30),
    _coordinator,
    ctx => new ManagerApprovalRequest("REQ-1", "PART-99", 199.99m),
    resultContextKey: "parts.approval.result"));
```

### 3. Host UI Consumption (`Index.razor`)
In the Blazor host page:
```csharp
<CustomChatWindowV3
    Runtime="ConversationRuntime"
    Style="ChatStyle.SidebarChat"
    OnHostOutput="HandleHostOutputAsync" />

@code {
    private async Task HandleHostOutputAsync(ConversationHostOutputContext context)
    {
        switch (context.Output)
        {
            case HostNotification<InventoryAlertNotification> notification:
                // Update host UI banner / state
                break;

            case HostInteractionRequest<ManagerApprovalRequest, ManagerApprovalResponse> request:
                // Show host dialog, then respond:
                await context.RespondAsync(new ManagerApprovalResponse(true, "Manager John", null));
                break;
        }
    }
}
```

