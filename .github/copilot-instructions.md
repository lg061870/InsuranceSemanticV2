# ConversaCore AI Coding Agent Instructions

## Project Architecture Overview

This is a **conversational AI framework** built around the **ConversaCore** library with an **InsuranceAgent** implementation. The architecture follows an event-driven, topic-based conversation flow pattern using state machines with **hand-down/regain control** mechanisms for complex nested workflows.

### Core Components

- **ConversaCore/**: Framework library providing topic flows, activities, state machines, and event system
- **InsuranceAgent/**: Blazor Server web app implementing insurance-specific conversation topics  
- **WorkflowEditor/**: Component library for workflow visualization/editing

### Key Architecture Principles

- **Sub-Topic Composition**: Topics can call sub-topics and wait for completion using `waitForCompletion: true`
- **Event-Driven Coordination**: `InsuranceAgentService` orchestrates topic lifecycle through event handlers
- **Complex Decision Trees**: Use `ConditionalActivity` + `CompositeActivity` for multi-path compliance flows

## Critical Patterns & Conventions

### Topic-Based Conversation Architecture

Topics are the central unit of conversation logic. Each topic inherits from `TopicFlow` and contains a queue of activities:

```csharp
// Topic registration pattern in AddInsuranceTopics.cs
services.AddScoped<ITopic>(sp => new BeneficiaryInfoDemoTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<BeneficiaryInfoDemoTopic>>(),
    sp.GetRequiredService<IConversationContext>()
));
```

**Key dependencies**: All custom topics require `TopicWorkflowContext`, `ILogger<T>`, and optionally `IConversationContext`.

### Hand-Down/Regain Control Pattern (NEW)

Topics can now call sub-topics and wait for completion instead of terminating:

```csharp
// Call sub-topic and wait for completion
Add(new TriggerTopicActivity("CollectCompliance", "ComplianceTopic", _logger, 
    waitForCompletion: true));  // KEY: Wait instead of hand-off

// This ONLY executes after sub-topic completes  
Add(new SimpleActivity("ProcessResults", (ctx, input) => {
    var data = ctx.GetValue<object>("SubTopicCompletionData");
    // Process completion data...
}));
```

**Critical**: Use `CompleteTopicActivity` to properly signal completion in sub-topics.

### Activity Queue Pattern

Topics execute activities in FIFO order. Common activity types:
- `AdaptiveCardActivity<TCard, TModel>`: Renders cards and waits for input
- `TriggerTopicActivity`: Chains to next topic (legacy) or calls sub-topic (new `waitForCompletion: true`)
- `CompleteTopicActivity`: Signals topic completion for hand-down pattern
- `ConditionalActivity<T>`: Complex decision branches with typed inner activities
- `CompositeActivity`: Groups multiple activities into sequences
- `DumpCtxActivity`: Debug context dumping
- System activities: `SimpleActivity`, `DelayActivity`, `RepeatActivity`

### State Machine Integration

Topics use `TopicStateMachine<TState>` with explicit transition configuration:

```csharp
_fsm.ConfigureTransition(FlowState.Idle, FlowState.Starting);
_fsm.ConfigureTransition(FlowState.Running, FlowState.WaitingForInput);
```

### Service Registration Patterns

**CRITICAL**: Most services are **scoped**, not singleton, due to `IConversationContext` dependency:

```csharp
// In Program.cs
builder.Services.AddConversaCore();
builder.Services.AddScoped<ISemanticKernelService, SemanticKernelService>();
```

Topic registry setup **must be in a service scope** to avoid singleton/scoped conflicts.

## Development Workflows

### Building & Running
- **Solution**: `InsuranceSemantic.sln` (3 projects)
- **Target**: .NET 8.0 with nullable enabled
- **Web app**: Run `InsuranceAgent` project (Blazor Server on HTTPS)
- **Tests**: Use `runTests.ps1` for diagnostic logging or VS Test Explorer

### Topic Development Pattern
1. Create topic class inheriting `TopicFlow`
2. Add to DI in `AddInsuranceTopics()` method
3. Implement `CanHandleAsync()` for intent recognition
4. Build activity queue in constructor using `.Add(activity)`

### Intent Recognition
Uses Semantic Kernel with keyword fallback. Topics define `IntentKeywords` arrays and implement `CanHandleAsync()` returning confidence 0-1.

### Adaptive Cards
Cards follow the pattern: `Card + Model + Activity`:
- Card class creates JSON schema
- Model defines data binding with validation attributes
- `AdaptiveCardActivity<TCard, TModel>` handles lifecycle

**Card ID & Render Modes**: Each adaptive card receives a unique ID that flows through all layers. This enables:
- `RenderMode.Replace`: Overwrites existing card with same ID (e.g., validation errors)
- `RenderMode.Append`: Adds new card to end of chat window
- Critical for maintaining clean UX when validation fails - user sees only one card instead of duplicates

### Complex Decision Trees

Use nested `ConditionalActivity` + `CompositeActivity` for compliance flows:

```csharp
// Multi-path decision tree example from InsuranceAgentService
IfCase("TCPA_YES_CCPA_YES", ctx =>
    IsYes(ctx,"tcpa_consent") && IsYes(ctx,"ccpa_acknowledgment"),
    ConditionalActivity<TopicFlowActivity>.If(
        "HAS_CA_INFO_YES_YES",
        c => c.GetValue<bool?>("is_california_resident").HasValue,
        (id,c) => ToMarketingT1Topic("CA_KNOWN_YES_YES"),
        (id,c) => new CompositeActivity("ASK_CA_YES_YES", new List<TopicFlowActivity>{
            AskCaliforniaResidency("CA_CARD_YES_YES","MarketingTypeOneTopic","full_with_ca_protection"),
            ToMarketingT1Topic("AFTER_CA_YES_YES")
        })
    )
)
```

## Integration Points

### Event System
Heavy use of C# events for component communication:
- `TopicLifecycleChanged`, `ActivityCompleted`, `ModelBound`
- `ITopicEventBus` for cross-topic messaging (singleton instance)

### Context Management
- `IConversationContext`: Per-conversation state (scoped)
- `TopicWorkflowContext`: Key-value store for topic data
- Context flows through activity pipeline automatically

### Semantic Kernel Integration
- Intent recognition via `IntentRecognitionService`
- Kernel configured as singleton, services as scoped
- Used for LLM-based topic routing and confidence scoring

## File Organization Conventions

### Topic Structure
```
Topics/
  TopicName/
    TopicNameDemoTopic.cs    # Main topic flow
    TopicNameCard.cs         # Adaptive card definition  
    TopicNameModels.cs       # Data models
```

### Activity Location
- Framework activities: `ConversaCore/TopicFlow/Activities/`
- App-specific activities: Typically embedded in topic classes

### System Topics
Built-in topics in `ConversaCore/SystemTopics/`: `FallbackTopic`, `OnErrorTopic`, `EscalateTopic`, etc.

## Common Debugging Patterns

- Enable `DumpCtxActivity` in development for context inspection
- Use structured logging with topic/activity names
- Check state machine transitions via `TransitionHistory`
- Event hooks provide detailed activity lifecycle visibility
- Topic registry must be configured in service scope to avoid DI conflicts

## Key Dependencies

- **Microsoft.SemanticKernel**: LLM integration (v1.64.0)
- **ASP.NET Core**: Blazor Server host
- **System components**: Heavy use of events, dependency injection, async patterns