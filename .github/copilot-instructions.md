
# ConversaCore/InsuranceSemantic AI Agent Instructions

## Architecture Overview

This solution is a modular conversational AI framework built around the **ConversaCore** library, with a Blazor Server implementation (`InsuranceAgent`) and a workflow visualization/editor (`WorkflowEditor`).

**Major components:**
- `ConversaCore/`: Core framework (topic flows, activities, state machines, event system)
- `InsuranceAgent/`: Blazor Server app with insurance-specific topics and orchestration
- `WorkflowEditor/`: Workflow visualization and editing components

**Key design:**
- **Topic-based flows:** Each conversation topic is a class inheriting `TopicFlow`, composed of a FIFO queue of activities (see `ConversaCore/TopicFlow/` and `InsuranceAgent/Topics/`).
- **Hand-down/regain control:** Topics can call sub-topics and resume after completion using `TriggerTopicActivity` with `waitForCompletion: true` and `CompleteTopicActivity`.
- **Event-driven:** Activities and topics communicate via C# events, with event bubbling through container activities (`ConditionalActivity`, `CompositeActivity`).
- **State machines:** Each topic uses an explicit state machine (`TopicStateMachine<TState>`) for flow control.

## Developer Workflows

- **Build:** Open `InsuranceSemantic.sln` in VS or use `dotnet build` (requires .NET 9.0.306, see `global.json`).
- **Run:** Start the `InsuranceAgent` project (Blazor Server, HTTPS by default).
- **Test:** Use `runTests.ps1` for full diagnostics, or run tests via VS Test Explorer. Tests are in `ConversaCore.Tests/` and use `RollForward=LatestMajor` for .NET 9 compatibility.
- **Debug:** Use `DumpCtxActivity` for context inspection; structured logging is enabled throughout.

## Project-Specific Patterns

- **Topic registration:** Register topics in DI with explicit logger pattern (see `AddInsuranceTopics.cs`). Example:
  ```csharp
  services.AddScoped<ILogger<MyTopic>>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<MyTopic>());
  services.AddScoped<ITopic>(sp => new MyTopic(
      sp.GetRequiredService<TopicWorkflowContext>(),
      sp.GetRequiredService<ILogger<MyTopic>>(),
      sp.GetRequiredService<IConversationContext>()
  ));
  ```
- **Activity types:**
  - `AdaptiveCardActivity<TCard, TModel>`: Renders adaptive cards, handles input/validation
  - `TriggerTopicActivity`: Calls sub-topics, optionally waits for completion
  - `EventTriggerActivity`: Triggers UI events (fire-and-forget or wait-for-response)
  - `ConditionalActivity<T>`, `CompositeActivity`: For branching and grouping
  - `DumpCtxActivity`: Dumps context for debugging
- **Adaptive card UX:** Each card has a unique ID; use `RenderMode.Replace` to avoid duplicate cards on validation errors.
- **Intent recognition:** Topics implement `CanHandleAsync()` and define `IntentKeywords` for routing; uses Semantic Kernel with fallback.
- **Service lifetimes:** Most services are **scoped** (not singleton) due to context dependencies. Topic registry setup must occur in a service scope (see `Program.cs`).
- **Event interfaces:** Topics using dynamic activities should implement `ITopicTriggeredActivity` or `ICustomEventTriggeredActivity` for event forwarding.

## File/Directory Conventions

- **Topics:** `InsuranceAgent/Topics/TopicName/` (Demo topics: `InsuranceAgent/Topics/Demo/`)
- **Activities:** Framework: `ConversaCore/TopicFlow/Activities/`; app-specific: in topic classes
- **System topics:** `ConversaCore/SystemTopics/`
- **Tests:** `ConversaCore.Tests/`

## Integration & External Dependencies

- **Semantic Kernel:** LLM-based intent recognition and topic routing
- **SQLite:** Vector DB auto-initializes at `vectorstore.db`
- **Blazor Server:** Main UI host
- **xUnit, FluentAssertions, Moq:** Testing

## Common Pitfalls

- Topic registry/configuration must be done in a service scope after app build, before `app.Run()`
- Most services must be scoped, not singleton
- Event bubbling is required for correct UI/event handling
- Use `CompleteTopicActivity` to signal sub-topic completion

---
For more, see `AddInsuranceTopics.cs`, `Program.cs`, and demo topics in `InsuranceAgent/Topics/Demo/`.