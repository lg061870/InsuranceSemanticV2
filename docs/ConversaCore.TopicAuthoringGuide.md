# ConversaCore Topic Authoring Guide

The canonical developer and tooling guide for authoring conversational topics, tools, and host interactions in ConversaCore. This document defines the supported C# programming model for human domain developers, AI coding assistants, and visual diagram code generators (such as ScriptEditor).

---

## 1. Architectural Mental Model

ConversaCore strictly separates conversation execution into four decoupled layers:

```text
User / Chat UI (<CustomChatWindowV3>)
       │
       ▼  (IConversationRuntime)
Topics (ComposedTopicFlow)
       │
       ├─► Prompts & Adaptive Cards (IWorkflowActivityFactory)
       │
       ├─► Tools (InvokeToolActivity<TTool, TRequest, TResult> -> IToolExecutor)
       │     ├── Topic Allowlist (AllowedToolIds)
       │     └── Human Confirmation (ConfirmationGranted from User Action)
       │
       └─► Typed Host Boundary
             ├── One-Way: PublishHostNotificationActivity<TPayload>
             └── Correlated Two-Way: InvokeHostInteractionActivity<TRequest, TResponse>
```

### The 4 Layers

1. **Topics (`ComposedTopicFlow`)**: The conversation workflow. Topics coordinate dialogues, cards, conditional branching, tool invocations, and host events.
2. **Tools (`IConversaTool<TRequest, TResult>`)**: The real-world business actions (querying databases, calling REST APIs). Tools are completely decoupled from topics and UI.
3. **Contracts & Models**: Plain C# records with DataAnnotations (`[Required]`, `[Range]`) for card submissions, tool inputs/results, and host event payloads.
4. **Runtime & Presentation (`IConversationRuntime`, `ConversaCore.UI`)**: Manages conversation session lifetime, ordered output streaming, security validation, and Blazor UI rendering.

### The Activity Boundary Invariant
> [!IMPORTANT]
> **Domain developers and code generators must NEVER create custom `Activity` subclasses in domain projects.**
> All conversational activities are provided by the framework (`SimpleActivity`, `DelayActivity`, `PromptActivity`, `InvokeToolActivity`, `PublishHostNotificationActivity`, `InvokeHostInteractionActivity`, etc.). Domain projects contain only topics, tools, data models, and host UI event handlers.
>
> For code generators (such as ScriptEditor#46) and AI coding assistants emitting topic code, see the formal [Generator Integration Contract](ConversaCore.GeneratorIntegrationContract.md) for the exact activity constructor capability matrix and syntax emission rules.

---

## 2. Topic Descriptors & Registration

Every topic in ConversaCore has an identity, human-readable metadata, and optional trigger phrases configured at startup.

### Topic Registration
Topics are registered in dependency injection using `IConversaCoreBuilder`:

```csharp
using ConversaCore.Configuration;
using MyDomainAgent.Topics;

public static class ConversaCoreTopicRegistration
{
    public static IConversaCoreBuilder AddDomainTopics(this IConversaCoreBuilder builder)
    {
        // Register topic with unique ID and options
        builder.AddTopic<CheckInventoryTopic>("parts.inventory.check", options =>
        {
            options.DisplayName = "Check Parts Inventory";
            options.TriggerPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "check parts",
                "inventory",
                "stock check"
            };
        });

        return builder;
    }
}
```

### Topic Intent & Routing
Topics can implement `CanHandleAsync` to advertise their confidence in handling user input:

```csharp
public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return Task.FromResult(0.0f);
    }

    if (message.Contains("inventory", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("part", StringComparison.OrdinalIgnoreCase))
    {
        return Task.FromResult(0.9f);
    }

    return Task.FromResult(0.0f);
}
```

- Return a float between `0.0f` (cannot handle) and `1.0f` (exact match).
- `CanHandleAsync` must be fast, synchronous or non-blocking, and free of side effects.

---

## 3. The Composed Lifecycle (`ComposedTopicFlow`)

All new topics must inherit from `ComposedTopicFlow`.

### Key Design Principles:
1. **Explicit Constructor Injection**: Capture all required collaborators (workflow context, loggers, factories, executors, sessions) via standard constructor parameters. Store them in `private readonly` fields.
2. **Post-Construction Composition**: The activity graph is assembled inside the overridden `ComposeWorkflow()` method.
3. **Zero Constructor Work**: Never execute asynchronous operations, call virtual methods, or build activity graphs in the constructor.

```csharp
using ConversaCore.Authoring;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;

namespace MyDomainAgent.Topics;

public sealed class OrderFulfillmentTopic : ComposedTopicFlow
{
    private readonly IWorkflowActivityFactory _activities;
    private readonly IConversationSession _session;

    public OrderFulfillmentTopic(
        TopicWorkflowContext context,
        ILogger<OrderFulfillmentTopic> logger,
        IWorkflowActivityFactory activities,
        IConversationSession session)
        : base(context, logger, "orders.fulfillment")
    {
        _activities = activities;
        _session = session;
    }

    protected override void ComposeWorkflow()
    {
        Add(new SimpleActivity("greet", async (ctx, ct) =>
        {
            await ctx.SendBotMessageAsync("Welcome to Order Fulfillment!", ct);
            return null;
        }));

        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            "ask-order-id",
            "Please provide your order number.")));
    }
}
```

### Lifecycle Guarantees of `ComposedTopicFlow`:
- **Post-Construction Activation**: `TopicActivator` calls `InitializeAsync()` after DI construction completes.
- **Idempotency**: Repeated or concurrent initialization calls compose the graph exactly once.
- **Automatic Graph Reset**: On conversation reset, `ComposedTopicFlow` clears the activity graph and recomposes cleanly without requiring constructor reinvocation or reflection.

---

## 4. The Explicit Async-Initialization Exception

`ComposeWorkflow()` is intentionally **synchronous** because constructing in-memory activity graphs does not require I/O. Asynchronous operations are deferred until the workflow runner executes each activity.

### When Async Initialization is Allowed
In rare domain scenarios, a topic's activity graph structure depends dynamically on external asynchronous data loaded at activation time (for example, reading active underwriting rules from a database as demonstrated in `MarketingT1Topic`).

For these scenarios:
1. Derive directly from `TopicFlow` (instead of `ComposedTopicFlow`).
2. Implement `IAsyncInitializable`.
3. In `InitializeAsync(CancellationToken)`, perform the asynchronous fetch and build the activity graph.
4. **Never** block synchronously (`.Result`, `.Wait()`) or invoke `Task.Run` from a constructor.

```csharp
public sealed class DynamicRuleTopic : TopicFlow, IAsyncInitializable
{
    private readonly IRuleRepository _rules;
    private bool _initialized;

    public DynamicRuleTopic(
        TopicWorkflowContext context,
        ILogger<DynamicRuleTopic> logger,
        IRuleRepository rules)
        : base(context, logger, "dynamic.rules")
    {
        _rules = rules;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        var activeRules = await _rules.GetActiveRulesAsync(cancellationToken);
        foreach (var rule in activeRules)
        {
            Add(new SimpleActivity($"rule.{rule.Id}", async (ctx, ct) =>
            {
                await ctx.SendBotMessageAsync(rule.PromptText, ct);
                return null;
            }));
        }

        _initialized = true;
    }
}
```

---

## 5. State Management & Context Keys

Topics store conversational and execution state in `TopicWorkflowContext`.

### State Access
```csharp
// Store value
context.SetValue("user.email", "alex@example.com");

// Retrieve value
string? email = context.GetValue<string>("user.email");

// Try retrieve
if (context.TryGetValue<int>("retry.count", out var count))
{
    // ...
}
```

### Standard Context Keys
- **`modelContextKey`**: In adaptive card activities, specifies where the validated submission model is placed.
- **`resultContextKey`**: In tool activities (`InvokeToolActivity`), specifies where the `ToolResult<TResult>` is placed.

### Transcript Isolation
> [!NOTE]
> Internal workflow context state and host event payloads do **not** leak into the public conversation transcript. Standard chat messages and adaptive cards are projected to the user transcript, whereas host notifications and tool results remain isolated to the host boundary and workflow state.

---

## 6. Subtopics & Coordination

Complex domains should be divided into modular, single-responsibility topics.

### Hand-Down and Regain Control
A parent topic invokes a subtopic using `TriggerTopicActivity`:

```csharp
// Parent Topic
Add(new TriggerTopicActivity(
    id: "trigger.payment",
    targetTopicId: "billing.payment",
    waitForCompletion: true));

Add(new SimpleActivity("after.payment", async (ctx, ct) =>
{
    var paymentConfirmed = ctx.GetValue<bool>("payment.success");
    await ctx.SendBotMessageAsync($"Payment status: {paymentConfirmed}", ct);
    return null;
}));
```

### Child Topic Completion
The child subtopic signals completion using `CompleteTopicActivity`:

```csharp
// Child Topic (billing.payment)
Add(new CompleteTopicActivity(
    id: "payment.done",
    resultContextKey: "payment.success",
    resultValue: true));
```

---

## 7. Cards: Generated vs. Rich Domain Cards

ConversaCore supports two complementary card patterns:

### Option A: Generated Adaptive Cards (Recommended for Form Inputs)
For standard data entry (text, numbers, dates, toggles, choices), use `GeneratedAdaptiveCardDefinition` with `IWorkflowActivityFactory`.

```csharp
// 1. Define typed model with DataAnnotations
public sealed record ShippingAddressModel
{
    [Required]
    [StringLength(100)]
    public string Street { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Invalid Zip Code")]
    public string ZipCode { get; set; } = string.Empty;

    public bool IsCommercial { get; set; }
}

// 2. In Topic ComposeWorkflow():
Add(_activities.CreateAdaptiveCard<ShippingAddressModel>(new GeneratedAdaptiveCardDefinition(
    id: "shipping.card",
    fields:
    [
        new GeneratedAdaptiveCardFieldDefinition(
            nameof(ShippingAddressModel.Street), "Street Address", GeneratedAdaptiveCardInputKind.Text, isRequired: true),
        new GeneratedAdaptiveCardFieldDefinition(
            nameof(ShippingAddressModel.City), "City", GeneratedAdaptiveCardInputKind.Text, isRequired: true),
        new GeneratedAdaptiveCardFieldDefinition(
            nameof(ShippingAddressModel.ZipCode), "Zip Code", GeneratedAdaptiveCardInputKind.Text, isRequired: true),
        new GeneratedAdaptiveCardFieldDefinition(
            nameof(ShippingAddressModel.IsCommercial), "Commercial Address?", GeneratedAdaptiveCardInputKind.Toggle)
    ],
    title: "Enter Shipping Address",
    submitLabel: "Save Address",
    modelContextKey: "shipping.address",
    isRequired: true)));
```

**Benefits:**
- Strongly typed `TModel` validated via standard `System.ComponentModel.DataAnnotations`.
- Safe: allowlists 5 specific input kinds (`Text`, `Number`, `Date`, `Toggle`, `Choice`).
- No hand-written JSON or dynamic dictionaries.

### Option B: Rich Domain Cards
When domain logic requires specialized card layouts beyond the standard input kinds (e.g. customized multi-tier insurance quote comparisons with interactive SVG charts):
- Author a dedicated card factory implementing `IAdaptiveCardFactory<TModel>`.
- Use `new AdaptiveCardActivity<TCard, TModel>(...)`.
- Preserves full typing while enabling arbitrary visual layouts.

---

## 8. Typed Tools & Bounded Selection

Tools allow topics to interact with databases, web services, and domain backends safely.

### 1. Authoring a Tool
Implement `IConversaTool<TRequest, TResult>`:

```csharp
using System.ComponentModel.DataAnnotations;
using ConversaCore.Tools;

namespace MyDomainAgent.Tools;

public sealed record LookupItemRequest
{
    [Required]
    public string ItemId { get; set; } = string.Empty;
}

public sealed record LookupItemResult(string ItemId, string Name, decimal Price, int InStock);

public sealed class LookupItemTool : IConversaTool<LookupItemRequest, LookupItemResult>
{
    public const string ToolId = "catalog.item.lookup";

    public static readonly ToolDescriptor Descriptor = new(
        toolId: ToolId,
        version: "1.0",
        displayName: "Lookup Item",
        description: "Looks up pricing and inventory for a catalog item.",
        requestType: typeof(LookupItemRequest),
        resultType: typeof(LookupItemResult),
        sideEffect: ToolSideEffect.ReadOnly,
        confirmation: new ToolConfirmationPolicy { Required = false },
        reliability: new ToolReliabilityPolicy { Timeout = TimeSpan.FromSeconds(5) },
        implementationType: typeof(LookupItemTool));

    ToolDescriptor IConversaTool<LookupItemRequest, LookupItemResult>.Descriptor => Descriptor;

    public async ValueTask<ToolResult<LookupItemResult>> ExecuteAsync(
        LookupItemRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Perform real lookup (e.g. DB query)
        var result = new LookupItemResult(request.ItemId, "Industrial Brake Pad", 89.95m, 14);
        return ToolResult<LookupItemResult>.Success(result);
    }
}
```

### 2. Invoking Tools in Topics
Always invoke tools through `InvokeToolActivity<TTool, TRequest, TResult>`:

```csharp
Add(new InvokeToolActivity<LookupItemTool, LookupItemRequest, LookupItemResult>(
    id: "invoke.lookup",
    toolId: LookupItemTool.ToolId,
    toolExecutor: _toolExecutor,
    requestFactory: ctx => new LookupItemRequest
    {
        ItemId = ctx.GetValue<string>("selected.item.id")!
    },
    executionContextFactory: _ => new ToolExecutionContext
    {
        ConversationId = _session.ConversationId,
        Subject = _session.Subject ?? "user",
        CorrelationId = Guid.NewGuid().ToString("N"),
        Services = _services,
        // STRICT TOPIC ALLOWLIST:
        AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LookupItemTool.ToolId
        }
    },
    resultContextKey: "lookup.result"));
```

### 3. Strict Mutation & Confirmation Policy
> [!CAUTION]
> Mutating tools (`ToolSideEffect.Mutating`) requiring confirmation (`ToolConfirmationPolicy { Required = true }`) **must** set `ConfirmationGranted = true` on `ToolExecutionContext`.
> **CRITICAL RULE**: `ConfirmationGranted` must be derived **strictly** from deterministic human action (e.g., user clicking a "Confirm" QuickAnswer button or submitting an approval card). **Never allow an LLM or model text generation to grant confirmation.**

---

## 9. Typed Host Outputs (Notifications & Correlated Interactions)

When a topic needs to communicate with the host application shell (Blazor page, hosting dashboard, side panel), use typed host outputs.

### 1. Define Immutable Contracts
```csharp
namespace MyDomainAgent.Contracts;

// One-way notification
public sealed record StatusChangedNotification(string TicketId, string NewStatus);

// Correlated interaction
public sealed record ManagerApprovalRequest(string RequestId, decimal Amount);
public sealed record ManagerApprovalResponse(bool Approved, string ApproverNotes);
```

### 2. Topic Activities
In your `ComposedTopicFlow`:

```csharp
// 1. One-way notification:
Add(new PublishHostNotificationActivity<StatusChangedNotification>(
    id: "notify.status",
    eventName: "ticket.status.changed",
    version: 1,
    payloadFactory: ctx => new StatusChangedNotification("T-100", "InReview"),
    dispatcher: _dispatcher,
    session: _session));

// 2. Correlated two-way interaction (topic awaits host response):
Add(new InvokeHostInteractionActivity<ManagerApprovalRequest, ManagerApprovalResponse>(
    id: "request.approval",
    requestType: "manager.approval",
    version: 1,
    timeout: TimeSpan.FromSeconds(60),
    coordinator: _coordinator,
    requestFactory: ctx => new ManagerApprovalRequest("REQ-101", 1500.00m),
    resultContextKey: "approval.result"));
```

### 3. Host UI Handling (`Index.razor`)
In the host page:

```razor
<CustomChatWindowV3
    Runtime="ConversationRuntime"
    OnHostOutput="HandleHostOutputAsync" />

@code {
    private async Task HandleHostOutputAsync(ConversationHostOutputContext context)
    {
        switch (context.Output)
        {
            case HostNotification<StatusChangedNotification> notification:
                // Update UI badge or banner
                break;

            case HostInteractionRequest<ManagerApprovalRequest, ManagerApprovalResponse> request:
                // Present modal dialog to user, then respond:
                await context.RespondAsync(new ManagerApprovalResponse(true, "Approved by Ops"));
                break;
        }
    }
}
```

---

## 10. Cancellation & Reset Lifecycle

ConversaCore enforces clean circuit isolation and reset lifecycles.

### Reset Semantics (`IConversationRuntime.ResetAsync`)
When a conversation is reset (e.g., user clicks "Restart" or a retry flow triggers):
1. **Cancellation**: Active running tasks, pending prompt waits, and awaiting host interactions are cancelled immediately.
2. **Workflow Cleanout**: In-memory workflow variables and activity executions are cleared.
3. **Recomposition**: `ComposedTopicFlow` recomposes its activity graph cleanly from the overridden `ComposeWorkflow()` method.
4. **Card State Reset**: Generated cards reset to a runnable `Created` state with pristine fields.
5. **No Constructor Rebuild**: Reset does not instantiate a new topic via DI or reflection; it uses the existing topic instance's composed reset lifecycle.

---

## 11. Prohibited Patterns ("Never Do This")

To guarantee testability, maintainability, and security across ConversaCore agents, observe these strict negative rules:

| Prohibited Pattern | Why It Is Prohibited | Supported Alternative |
|---|---|---|
| **Subclassing `TopicFlowActivity` in domain code** | Bypasses framework lifecycle, leaks internals, creates maintenance debt. | Use built-in activities (`SimpleActivity`, `InvokeToolActivity`, etc.). |
| **Using Service Locator inside activities** (`ctx.Services.GetService<T>()`) | Breaks static verification, masks missing dependencies. | Use explicit constructor injection in the topic. |
| **Async work or blocking calls in constructors** | Causes deadlocks, thread pool starvation, unhandled startup exceptions. | Use `ComposeWorkflow()` for synchronous graph building, or `IAsyncInitializable` for dynamic loading. |
| **Unrestricted Tool Execution** (Empty or omitted `AllowedToolIds`) | Allows prompt injection or unauthorized tool calls. | Always declare an explicit `AllowedToolIds` set. |
| **Model-inferred confirmation for mutating tools** | Allows AI hallucinations to commit real-world mutations without human consent. | Derive `ConfirmationGranted` strictly from user button/card clicks. |
| **Legacy `EventTriggerActivity`** | Untyped, leaks context, blocks topics on consumer delegates. | Use `PublishHostNotificationActivity` or `InvokeHostInteractionActivity`. |
| **Leaking `TopicWorkflowContext` to host UI** | Violates layer boundary and thread safety. | Use typed host notification and interaction contracts. |
| **Runtime JSON workflow interpreters** | Slow, untyped, difficult to debug and secure. | Visual authoring tools emit compilable C# targeting standard contracts. |

---

## 12. Recommended File Layout

In domain projects or template hosts:

```text
MyDomainAgent/
├── Contracts/
│   └── HostContracts.cs            # Host notification & interaction records
├── Models/
│   └── InputModels.cs              # Card submission models with DataAnnotations
├── Tools/
│   ├── CheckInventoryTool.cs       # Tool implementation & static Descriptor
│   └── RequestsAndResults.cs       # Tool request/result records
├── Topics/
│   ├── CheckInventoryTopic.cs      # Topic inheriting from ComposedTopicFlow
│   └── OrderFulfillmentTopic.cs
└── Configuration/
    └── ConversaCoreTopicRegistration.cs # DI topic & tool registrations
```
