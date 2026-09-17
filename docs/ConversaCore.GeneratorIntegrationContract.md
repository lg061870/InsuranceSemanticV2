# ConversaCore Generator Integration Contract

**Status:** Accepted Architecture Contract  
**Tracking:** [Parent #84 (WP6)](https://github.com/lg061870/InsuranceSemanticV2/issues/84), [Master #12](https://github.com/lg061870/InsuranceSemanticV2/issues/12), [Architecture Amendment #115](https://github.com/lg061870/InsuranceSemanticV2/issues/115), [Issue #90 (CC-605)](https://github.com/lg061870/InsuranceSemanticV2/issues/90)  
**Cross-Repository Consumer:** [ScriptEditor#46](https://github.com/lg061870/ScriptEditor/issues/46)

---

## 1. Purpose & Pipeline Architecture

This document specifies the formal integration contract between visual diagramming/authoring tools (such as **ScriptEditor**) and the **ConversaCore** conversational runtime.

### 1.1 The Visual-to-Runtime Pipeline

The supported authoring and execution pipeline is strictly compilable C#:

```text
┌──────────────────────────┐
│  Visual Diagram (JSON)   │  (Authored in ScriptEditor canvas)
└─────────────┬────────────┘
              │  ScriptEditor Roslyn Code Emitter
              ▼
┌──────────────────────────┐
│   Generated C# Source    │  (Topics, Models, Tools, DI Extensions)
└─────────────┬────────────┘
              │  Roslyn Compiler (Build or dynamic assembly)
              ▼
┌──────────────────────────┐
│   Compiled CLR Types     │  (Assembly with valid metadata)
└─────────────┬────────────┘
              │  ConversaCore DI & Scoped Activation
              ▼
┌──────────────────────────┐
│   IConversationRuntime   │  (Ordered execution, typed outputs, reset lifecycle)
└──────────────────────────┘
```

### 1.2 Core Architectural Invariant: No Runtime Diagram Interpretation
> [!IMPORTANT]
> **ConversaCore does not interpret visual diagram JSON at runtime.**  
> ScriptEditor owns diagram interpretation and emits standard, strongly typed C# source code. ConversaCore provides the runtime abstractions, lifecycle contracts, and activity factories that the generated C# compiles against. ConversaCore never inspects diagram files, parses graph JSON, or executes untyped node graphs dynamically.

---

## 2. Ownership Boundary (ScriptEditor#46 vs. ConversaCore)

| Concern | ScriptEditor Responsibility | ConversaCore Responsibility |
|---|---|---|
| **Visual Diagramming** | Owns diagram JSON schema, canvas rendering, node/port layout, and connectivity validation. | None. Unaware of diagram presentation or layout. |
| **Code Generation** | Emits clean, compilable C# source using Roslyn syntax trees for topics, models, tools, and registrations. | Exposes stable, typed public contracts and activity definitions that generated C# targets. |
| **Type Compilation** | Compiles emitted C# via Roslyn or MSBuild into runnable assemblies. | Consumes compiled topic types through standard Microsoft.Extensions.DependencyInjection. |
| **Topic Lifecycle** | Emits topics deriving from `ComposedTopicFlow` with explicit constructor injection. | `TopicActivator` instantiates topics via DI and invokes `InitializeAsync()` post-construction. |
| **Activity Instantiation** | Emits direct activity constructors or calls to `IWorkflowActivityFactory`. | Supplies sealed framework activities and validates activity definitions at construction time. |
| **Tool Execution** | Emits `InvokeToolActivity` with strict topic allowlists (`AllowedToolIds`). | `IToolExecutor` enforces policy, schema validation, authorization, confirmation, and timeout. |
| **Host Boundary** | Emits typed host notification and interaction activities. | Routes immutable outputs through `IConversationOutputDispatcher` and `IHostInteractionCoordinator`. |
| **State & Presentation** | None. | Manages session state, ordered streaming, Blazor presentation, and circuit reset. |

### Strict Negative Rules for Code Generators
Generated code must **NEVER**:
1. **Subclass `TopicFlowActivity`**: Generators must only instantiate framework-supplied activities.
2. **Subclass Legacy Agent Base Classes**: Never generate classes deriving from `DomainAgentService` or `InsuranceAgentService`.
3. **Use Ambient Service Locators**: Never emit `ctx.Services.GetService<T>()` or static service locators inside activity lambdas.
4. **Perform Async Operations in Constructors**: Constructors must only assign injected fields.
5. **Bypass Confirmation**: Mutating tools must derive confirmation from human user selection, never from LLM text.
6. **Emit Untyped Event Handlers**: Never use obsolete `EventTriggerActivity`.
7. **Leak Workflow Context**: Never expose `TopicWorkflowContext` across the host output boundary.

---

## 3. Activity Constructor Capability Matrix

ScriptEditor and code generators must follow this precise capability matrix when mapping visual diagram nodes to C# syntax:

| Node / Activity Kind | Target C# Syntax | Construction Pattern | Collaborator Source |
|---|---|---|---|
| **Message / Simple Step** | `SimpleActivity` | `new SimpleActivity(id, async (ctx, ct) => { ... })` | Literal / inline lambda |
| **Delay / Pause** | `DelayActivity` | `new DelayActivity(id, TimeSpan.FromSeconds(n))` | Literal-safe parameter |
| **Topic Complete** | `CompleteTopicActivity` | `new CompleteTopicActivity(id, resultContextKey, resultValue)` | Literal-safe parameter |
| **End Conversation** | `EndActivity` | `new EndActivity(id)` | Literal-safe parameter |
| **Fallback Branch** | `FallbackActivity` | `new FallbackActivity(id, maxAttempts, fallbackAction)` | Literal / lambda |
| **Reset Conversation** | `ResetActivity` | `new ResetActivity(id)` | Literal-safe parameter |
| **Prompt / Question** | `PromptActivityDefinition` | `_activities.CreatePrompt(new PromptActivityDefinition(id, promptText))` | Scoped `IWorkflowActivityFactory` |
| **Quick Answer Options** | `QuickAnswerActivityDefinition` | `_activities.CreateQuickAnswer(new QuickAnswerActivityDefinition(id, question, options))` | Scoped `IWorkflowActivityFactory` |
| **Adaptive Card Form** | `GeneratedAdaptiveCardDefinition` | `_activities.CreateAdaptiveCard<TModel>(new GeneratedAdaptiveCardDefinition(...))` | Scoped `IWorkflowActivityFactory` + Generated Model `TModel` |
| **Tool Execution** | `InvokeToolActivity<TTool, TReq, TRes>` | `new InvokeToolActivity<TTool, TReq, TRes>(id, toolId, _toolExecutor, ...)` | Generic type parameters + Scoped `IToolExecutor` |
| **Host Notification** | `PublishHostNotificationActivity<TPayload>` | `new PublishHostNotificationActivity<TPayload>(id, eventName, version, ...)` | Generic payload + `IConversationOutputDispatcher` |
| **Host Interaction** | `InvokeHostInteractionActivity<TReq, TResp>` | `new InvokeHostInteractionActivity<TReq, TResp>(id, requestType, version, timeout, _coordinator, ...)` | Generic contracts + `IHostInteractionCoordinator` |
| **Subtopic Hand-Down** | `TriggerTopicActivity` | `new TriggerTopicActivity(id, targetTopicId, waitForCompletion: true)` | Literal topic ID string |
| **Conditional Branch** | `ConditionalActivity` | `new ConditionalActivity(id, predicateFunc, ifTrueActivity, ifFalseActivity)` | Emitted predicate lambda |
| **Switch Branch** | `SwitchActivity` | `new SwitchActivity(id, selectorFunc, branchesDictionary)` | Emitted selector lambda |

---

## 4. Explicit DI & Constructor Emission Rules

Generated topics must adhere to the explicit constructor dependency injection pattern.

### 4.1 Required Constructor Shape
The generator must emit a primary constructor capturing only required collaborators into private readonly fields:

```csharp
public sealed class GeneratedCheckoutTopic : ComposedTopicFlow
{
    private readonly IWorkflowActivityFactory _activities;
    private readonly IToolExecutor _toolExecutor;
    private readonly IConversationSession _session;
    private readonly IServiceProvider _services;

    public GeneratedCheckoutTopic(
        TopicWorkflowContext context,
        ILogger<GeneratedCheckoutTopic> logger,
        IWorkflowActivityFactory activities,
        IToolExecutor toolExecutor,
        IConversationSession session,
        IServiceProvider services)
        : base(context, logger, "checkout.topic")
    {
        _activities = activities;
        _toolExecutor = toolExecutor;
        _session = session;
        _services = services;
    }

    protected override void ComposeWorkflow()
    {
        // Assemble in-memory activity graph here synchronously
    }
}
```

### 4.2 Supported Injected Collaborators
Generators may only inject recognized scoped services:
- `TopicWorkflowContext` *(required base)*
- `ILogger<TTopic>` *(required base)*
- `IWorkflowActivityFactory` *(for prompts, quick answers, generated adaptive cards)*
- `IToolExecutor` *(for `InvokeToolActivity`)*
- `IConversationSession` *(for conversation metadata, subject, conversation ID)*
- `IConversationOutputDispatcher` *(for `PublishHostNotificationActivity`)*
- `IHostInteractionCoordinator` *(for `InvokeHostInteractionActivity`)*
- `IServiceProvider` *(for passing to `ToolExecutionContext.Services`)*

### 4.3 DI Registration Emission
Generators should emit an `IConversaCoreBuilder` extension method to register the generated topics and tools cleanly:

```csharp
public static class GeneratedTopicRegistration
{
    public static IConversaCoreBuilder AddGeneratedTopics(this IConversaCoreBuilder builder)
    {
        builder.AddTopic<GeneratedCheckoutTopic>("checkout.topic", options =>
        {
            options.DisplayName = "Checkout Flow";
            options.TriggerPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "checkout",
                "buy now",
                "place order"
            };
        });

        return builder;
    }
}
```

---

## 5. Bounded Definitions & Typed Form Models

ScriptEditor emits strongly typed C# records for form data, which ConversaCore validates via standard DataAnnotations.

### 5.1 Model Generation Rules
1. Every card form must have a corresponding concrete C# record or class.
2. Fields must include appropriate DataAnnotations:
   - `[Required]` for non-optional fields.
   - `[Range(min, max)]` for numeric fields.
   - `[StringLength(max)]` for text fields.
   - `[RegularExpression(...)]` for formatted inputs (email, zip code, phone).

```csharp
public sealed record CustomerContactModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress(ErrorMessage = "Valid email required.")]
    public string Email { get; set; } = string.Empty;

    [Range(18, 120, ErrorMessage = "Age must be between 18 and 120.")]
    public int Age { get; set; }

    public bool AcceptTerms { get; set; }
}
```

### 5.2 `GeneratedAdaptiveCardDefinition` Bounds Enforced by Framework
The generator must respect the runtime bounds enforced by `GeneratedAdaptiveCardDefinition`:
- **Allowed Input Kinds**: Text, Number, Date, Toggle, Choice (`GeneratedAdaptiveCardInputKind`).
- **Field Limit**: Maximum 64 fields per card.
- **Choice Limit**: Maximum 100 choices per choice field.
- **Field ID Length**: Maximum 128 characters, unique within the card (ordinal case-insensitive).
- **Label / Placeholder Length**: Maximum 1,024 characters.
- **Choice Value Length**: Maximum 256 characters.
- **Adaptive Card Version**: Deterministically serialized as version 1.3 by ConversaCore.

---

## 6. Bounded Tool Invocation & Confirmation Policies

When a visual diagram includes a tool invocation step:

### 6.1 Allowlist Enforcement
The generator must emit an explicit `AllowedToolIds` set on `ToolExecutionContext`. Unregistered or undeclared tool IDs are rejected unconditionally by `IToolExecutor`:

```csharp
executionContextFactory: _ => new ToolExecutionContext
{
    ConversationId = _session.ConversationId,
    Subject = _session.Subject ?? "user",
    CorrelationId = Guid.NewGuid().ToString("N"),
    Services = _services,
    AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CreateOrderTool.ToolId
    }
}
```

### 6.2 Mutation Safety & Human Confirmation
For mutating tools (`ToolSideEffect.Mutating`) requiring confirmation:
- The generator must ensure `ConfirmationGranted` is mapped from explicit human action (such as a previous card submission or QuickAnswer button click).
- **Hard Rule**: The generator must NEVER generate logic that allows LLM output text or semantic completion to set `ConfirmationGranted = true`.

---

## 7. Complete Reference Generated Topic

Below is the canonical reference code shape that ScriptEditor should emit for an end-to-end flow:

```csharp
// <auto-generated>
// Generated by ScriptEditor for ConversaCore
// </auto-generated>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Authoring;
using ConversaCore.Configuration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Tools;
using Microsoft.Extensions.Logging;

namespace MyDomain.Generated;

// 1. Typed Card Model
public sealed record PartLookupModel
{
    [Required]
    public string PartNumber { get; set; } = string.Empty;
}

// 2. Generated Topic
public sealed class PartInquiryTopic : ComposedTopicFlow
{
    private readonly IWorkflowActivityFactory _activities;
    private readonly IToolExecutor _toolExecutor;
    private readonly IConversationSession _session;
    private readonly IServiceProvider _services;

    public PartInquiryTopic(
        TopicWorkflowContext context,
        ILogger<PartInquiryTopic> logger,
        IWorkflowActivityFactory activities,
        IToolExecutor toolExecutor,
        IConversationSession session,
        IServiceProvider services)
        : base(context, logger, "parts.inquiry")
    {
        _activities = activities;
        _toolExecutor = toolExecutor;
        _session = session;
        _services = services;
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        if (message.Contains("part", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(0.9f);
        }
        return Task.FromResult(0.0f);
    }

    protected override void ComposeWorkflow()
    {
        // Step 1: Prompt user with adaptive card
        Add(_activities.CreateAdaptiveCard<PartLookupModel>(new GeneratedAdaptiveCardDefinition(
            id: "parts.card",
            fields:
            [
                new GeneratedAdaptiveCardFieldDefinition(
                    nameof(PartLookupModel.PartNumber),
                    "Part Number",
                    GeneratedAdaptiveCardInputKind.Text,
                    isRequired: true)
            ],
            title: "Check Part Inventory",
            submitLabel: "Lookup",
            modelContextKey: "parts.lookup.model",
            isRequired: true)));

        // Step 2: Invoke Tool
        Add(new InvokeToolActivity<PartLookupTool, PartLookupRequest, PartLookupResult>(
            id: "parts.tool.call",
            toolId: PartLookupTool.ToolId,
            toolExecutor: _toolExecutor,
            requestFactory: ctx =>
            {
                var model = ctx.GetValue<PartLookupModel>("parts.lookup.model")!;
                return new PartLookupRequest { PartNumber = model.PartNumber };
            },
            executionContextFactory: _ => new ToolExecutionContext
            {
                ConversationId = _session.ConversationId,
                Subject = _session.Subject ?? "user",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _services,
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    PartLookupTool.ToolId
                }
            },
            resultContextKey: "parts.tool.result"));

        // Step 3: Complete flow and output result
        Add(new SimpleActivity("parts.finish", async (ctx, ct) =>
        {
            var result = ctx.GetValue<ToolResult<PartLookupResult>>("parts.tool.result");
            var message = result?.IsSuccess == true
                ? $"Part {result.Value.PartNumber} is in stock: {result.Value.InStock} units."
                : "Unable to find the specified part.";

            await ctx.SendBotMessageAsync(message, ct);
            return null;
        }));
    }
}
```
