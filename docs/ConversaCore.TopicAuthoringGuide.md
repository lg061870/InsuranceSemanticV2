# ConversaCore Topic Authoring Guide (Draft)

This guide summarizes how to build conversational topics using the ConversaCore framework. It is intentionally short and focused so it can be embedded into tools.

---

## 1. Topic Basics

- **Topic class**: Derive from `ConversaCore.TopicFlow.Core.TopicFlow`.
- **Constructor**: Accept `TopicWorkflowContext`, `ILogger<TopicNameTopic>`, and `IConversationContext`, and call the base constructor.
- **Workflow**: In the constructor (or a helper), enqueue activities using the `Add(...)` API.
- **State machine**: Use the built-in `FlowState` transitions unless you have a clear reason to extend them.

```csharp
public partial class QuoteIntroTopic : TopicFlow
{
    public QuoteIntroTopic(
        TopicWorkflowContext context,
        ILogger<QuoteIntroTopic> logger,
        IConversationContext conversationContext)
        : base(context, logger, conversationContext)
    {
        BuildWorkflow();
    }
}
```

---

## 2. Allowed Activities (Core)

Use only the existing activity types from ConversaCore; do **not** introduce new activity base classes in domain code.

Common activities:

- `SimpleActivity` – inline C# lambda for quick logic or messages.
- `DelayActivity` – pause between messages for better pacing.
- `AdaptiveCardActivity<TCard, TModel>` – collect structured input via adaptive cards.
- `TriggerTopicActivity` – hand down control to a sub-topic (optionally wait for completion).
- `CompositeActivity` – group a sequence of activities.
- `ConditionalActivity<T>` – branch based on context or model state.
- `CompleteTopicActivity` – signal topic completion and optionally store a result payload.

Patterns:

```csharp
Add(SimpleActivity.Create("Intro", async (ctx, ct) =>
{
    await ctx.SendBotMessageAsync("Welcome to the quote helper!", ct);
}));

Add(DelayActivity.Create("IntroPause", TimeSpan.FromSeconds(1)));

Add(new AdaptiveCardActivity<QuoteStartCard, QuoteStartModel>(
    id: "QuoteStart",
    context: Context,
    cardFactory: card => card.Create("")));
```

---

## 3. Hand-Down / Regain Control

When one topic calls another and then resumes after it completes, follow this pattern:

1. **Parent topic** uses `TriggerTopicActivity` with `waitForCompletion: true`.
2. **Child topic** uses `CompleteTopicActivity` at the appropriate point to return control.
3. **Parent topic** reads any completion data from context after the trigger.

This keeps topic responsibilities clear and avoids deep coupling.

---

## 4. Intent & Routing

Each topic should implement `CanHandleAsync` to advertise how well it can handle a user input.

Guidelines:

- Use simple keyword / phrase checks first.
- Optionally call an LLM or embedding-based matcher if available.
- Return a score between 0.0 and 1.0; 0 means "cannot handle".

```csharp
public override Task<double> CanHandleAsync(string userInput, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(userInput)) return Task.FromResult(0.0);

    var text = userInput.ToLowerInvariant();
    if (text.Contains("quote") || text.Contains("price"))
    {
        return Task.FromResult(0.9);
    }

    return Task.FromResult(0.0);
}
```

Keep `CanHandleAsync` **fast** and side-effect free.

---

## 5. Authoring Constraints

When using tools (like the ConversaCore Topic Tool) or generating code:

- Do **not** modify ConversaCore framework projects.
- Do **not** add new base classes for activities or topics.
- Keep all domain logic in your app project (e.g., `InsuranceAgent` or SDK host).
- Prefer partial classes: one developer-owned file and one tool-generated file.
- Generated files should be safe to overwrite; never put manual code in them.

---

## 6. Recommended File Layout

For each topic `<TopicName>` in a host project:

- `Topics/<TopicName>/<TopicName>.Domain.cs` – developer-facing partial.
- `Topics/<TopicName>/<TopicName>.Generated.cs` – tool-generated partial.
- `Topics/<TopicName>/Cards/` – topic-specific card builders.
- `Topics/<TopicName>/Models/` – models bound to cards.
- `Topics/<TopicName>/Documents/` – documents associated with this topic (if any).

This layout matches the ConversaCore SDK template and Topic Tool expectations.

---

This document is a draft foundation for tooling prompts and should evolve alongside the SDK.
