
# ConversaCore/InsuranceSemantic AI Agent Instructions

## Architecture Overview

This solution is a modular conversational AI framework built around the **ConversaCore** library, with a Blazor Server implementation (`InsuranceAgent`) and a workflow visualization/editor (`WorkflowEditor`).

**Major components:**
- `ConversaCore/`: Core framework (topic flows, activities, state machines, event system)
- `InsuranceAgent/`: Blazor Server app with insurance-specific topics and orchestration
- `WorkflowEditor/`: Workflow visualization and editing components

**Key design principles:**
- **Topic-based flows:** Each conversation topic is a class inheriting `TopicFlow`, composed of a FIFO queue of activities (see `ConversaCore/TopicFlow/Core/TopicFlow.cs` and `InsuranceAgent/Topics/`). Activities are queued and executed sequentially.
- **Hand-down/regain control:** Topics can call sub-topics and resume after completion using `TriggerTopicActivity` with `waitForCompletion: true` and `CompleteTopicActivity`. See `hand-down-regain-control.md` for detailed patterns.
- **Event-driven:** Activities and topics communicate via C# events, with event bubbling through container activities (`ConditionalActivity`, `CompositeActivity`). Topics using dynamic activities must implement `ITopicTriggeredActivity` or `ICustomEventTriggeredActivity`.
- **State machines:** Each topic uses an explicit state machine (`TopicStateMachine<TState>`) for flow control. The base `TopicFlow` uses `FlowState` enum (Idle, Starting, Running, WaitingForInput, Completed, Failed).

## Developer Workflows

- **Build:** Open `InsuranceSemantic.sln` in VS or use `dotnet build` (requires .NET 9.0.306 per `global.json`).
- **Run:** Start the `InsuranceAgent` project (Blazor Server, HTTPS by default). Check startup diagnostics in console with timestamped logs showing DI registration phases.
- **Test:** Use `runTests.ps1` for full diagnostics (`--verbosity diagnostic`), or run tests via VS Test Explorer. Tests are in `ConversaCore.Tests/` and use `<RollForward>LatestMajor</RollForward>` in the `.csproj` to allow .NET 8 test runners to work with .NET 9 code.
- **Debug:** Use `DumpCtxActivity` (only outputs in dev mode) for context inspection. All services use structured logging via `ILogger<T>`. Startup logs include PID, thread ID, and timing information.

## Project-Specific Patterns

- **Topic registration:** Register topics in DI with explicit logger pattern (see `AddInsuranceTopics.cs`). Use helper function pattern:
  ```csharp
  void AddLogger<T>(IServiceCollection svc) where T : class
      => svc.AddScoped(_ => _.GetRequiredService<ILoggerFactory>().CreateLogger<T>());
  
  AddLogger<MyTopic>(services);
  services.AddScoped<ITopic>(sp => new MyTopic(
      sp.GetRequiredService<TopicWorkflowContext>(),
      sp.GetRequiredService<ILogger<MyTopic>>(),
      sp.GetRequiredService<IConversationContext>()
  ));
  ```
- **Activity types:**
  - `AdaptiveCardActivity<TCard, TModel>`: Renders adaptive cards, handles input/validation. Use with `cardFactory` and `modelContextKey` parameters.
  - `TriggerTopicActivity`: Calls sub-topics. Set `waitForCompletion: true` to resume after completion (hand-down/regain control pattern).
  - `CompleteTopicActivity`: **Required** to signal topic completion when using hand-down pattern; stores completion data in context.
  - `EventTriggerActivity`: Triggers UI events (fire-and-forget or wait-for-response). Implements `ICustomEventTriggeredActivity`.
  - `ConditionalActivity<T>`, `CompositeActivity`: For branching and grouping; implement both `ITopicTriggeredActivity` and `ICustomEventTriggeredActivity` for event bubbling.
  - `DumpCtxActivity`: Dumps context JSON for debugging (only active when `isDevelopment: true`).
  - `SimpleActivity`: Lambda-based activity for inline logic.
- **Adaptive card UX:** Each card has a unique ID; models are stored in context by `modelContextKey`. Validation errors auto-handled; card re-renders preserve state.
- **Intent recognition:** Topics implement `CanHandleAsync()` and define `IntentKeywords` static string array for routing. Uses Semantic Kernel with fallback.
- **Service lifetimes:** Most services are **scoped** (not singleton) due to context dependencies. `TopicWorkflowContext` and `IConversationContext` are scoped; `TopicRegistry` and `Kernel` are singleton. Topic registry configuration must occur in a service scope after app build (see `Program.cs` lines 71-85).
- **Event interfaces:** Topics using dynamic activities (like `ConditionalActivity` or `TriggerTopicActivity`) should implement `ITopicTriggeredActivity` or `ICustomEventTriggeredActivity` to enable event forwarding and bubbling.
- **State machine usage:** Custom topic states extend `TopicFlow.FlowState`. Configure valid transitions explicitly:
  ```csharp
  _fsm.ConfigureTransition(FlowState.Starting, FlowState.PromptingForRadioButtons);
  await _fsm.TryTransitionAsync(FlowState.PromptingForRadioButtons);
  ```

## File/Directory Conventions

- **Topics:** `InsuranceAgent/Topics/TopicName/` (Demo topics: `InsuranceAgent/Topics/Demo/` or root `InsuranceAgent/Topics/`)
- **Activities:** Framework: `ConversaCore/TopicFlow/Activities/`; app-specific: inline in topic classes
- **System topics:** `ConversaCore/SystemTopics/` (e.g., `FallbackTopic`, `ConversationStartTopic`, `OnErrorTopic`)
- **Tests:** `ConversaCore.Tests/` (xUnit, FluentAssertions, Moq)
- **Cards:** `InsuranceAgent/Cards/` for adaptive card builders; base classes in `ConversaCore/Cards/`

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