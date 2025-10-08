# ConversaCore AI Coding Agent Instructions

## Project Architecture Overview

This is a **conversational AI framework** built around the **ConversaCore** library with an **InsuranceAgent** implementation. The architecture follows an event-driven, topic-based conversation flow pattern using state machines.

### Core Components

- **ConversaCore/**: Framework library providing topic flows, activities, state machines, and event system
- **InsuranceAgent/**: Blazor Server web app implementing insurance-specific conversation topics
- **WorkflowEditor/**: Component library for workflow visualization/editing

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

### Activity Queue Pattern

Topics execute activities in FIFO order. Common activity types:
- `AdaptiveCardActivity<TCard, TModel>`: Renders cards and waits for input
- `TriggerTopicActivity`: Chains to next topic
- `DumpCtxActivity`: Debug context dumping
- System activities: `SimpleActivity`, `ConditionalActivity`, `DelayActivity`

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

## Key Dependencies

- **Microsoft.SemanticKernel**: LLM integration (v1.64.0)
- **ASP.NET Core**: Blazor Server host
- **System components**: Heavy use of events, dependency injection, async patterns