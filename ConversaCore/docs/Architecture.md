# ConversaCore Framework Architecture

**Version:** 1.0  
**Date:** October 7, 2025  
**Author:** Development Team  

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Core Architecture](#core-architecture)
3. [Framework Components](#framework-components)
4. [Activity System](#activity-system)
5. [Event-Driven Architecture](#event-driven-architecture)
6. [State Management](#state-management)
7. [Topic Flow System](#topic-flow-system)
8. [Adaptive Cards Integration](#adaptive-cards-integration)
9. [Service Layer](#service-layer)
10. [Extension Points](#extension-points)
11. [Design Patterns](#design-patterns)
12. [Best Practices](#best-practices)
13. [Implementation Guidelines](#implementation-guidelines)

## Executive Summary

ConversaCore is a sophisticated conversational AI framework designed for building complex, multi-turn conversations with rich user interactions. The framework combines **topic-based conversation flow**, **event-driven architecture**, and **adaptive card rendering** to create engaging, maintainable conversational experiences.

### Key Architectural Principles

- **Topic-Centric Design**: Conversations are organized around discrete topics with clear boundaries
- **Activity-Based Execution**: Each topic contains a queue of activities that execute in sequence
- **Event-Driven Communication**: Loose coupling through comprehensive event system
- **Adaptive UI Integration**: First-class support for rich, interactive user interfaces
- **State Machine Management**: Robust state tracking for complex conversation flows
- **Extensible Architecture**: Clean extension points for custom implementations

## Core Architecture

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Application Layer                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │   Blazor UI     │  │   Web API       │  │   Console App   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                    ConversaCore Framework                      │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                Service Layer                            │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │ Chat Service│  │Intent Recog.│  │ Navigation Svc  │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                │                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                Topic Flow Engine                        │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Topic Registry│  │ Activity    │  │ Flow Control    │ │   │
│  │  │             │  │ Execution   │  │ Activities      │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                │                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              Event & State Management                   │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Event Bus    │  │Conversation │  │ State Machine   │ │   │
│  │  │             │  │ Context     │  │                 │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                │                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                Core Activities                          │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Simple       │  │Adaptive Card│  │ Conditional     │ │   │
│  │  │Activity     │  │ Activity    │  │ Activity        │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Repeat       │  │Delay        │  │ Trigger Topic   │ │   │
│  │  │Activity     │  │Activity     │  │ Activity        │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Architectural Layers

#### 1. **Application Layer**
- Host applications (Blazor, Web API, Console)
- Application-specific UI components
- API controllers and endpoints

#### 2. **Service Layer**
- High-level orchestration services
- Integration with external systems
- Cross-cutting concerns (logging, configuration)

#### 3. **Topic Flow Engine**
- Core conversation management
- Topic registration and discovery
- Activity execution pipeline

#### 4. **Event & State Management**
- Event-driven communication
- Conversation state persistence
- State machine transitions

#### 5. **Core Activities**
- Reusable conversation building blocks
- Flow control primitives
- UI interaction components

## Framework Components

### Topic Flow System

The Topic Flow System is the heart of ConversaCore, managing conversation structure and execution.

```csharp
// Topic Flow Hierarchy
TopicFlow (Abstract Base)
├── SystemTopics/
│   ├── ConversationStartTopic
│   ├── FallbackTopic
│   ├── OnErrorTopic
│   └── EndOfConversationTopic
└── Custom Topics/
    ├── BeneficiaryInfoTopic
    ├── InsuranceNeedsTopic
    └── HealthQuestionnaireTopic
```

#### Topic Lifecycle

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│    Idle     │───▶│   Starting  │───▶│   Running   │───▶│  Completed  │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
       │                   │                   │                   │
       │                   ▼                   ▼                   │
       │            ┌─────────────┐    ┌─────────────┐              │
       │            │   Failed    │    │WaitingInput │              │
       │            └─────────────┘    └─────────────┘              │
       │                   │                   │                   │
       └───────────────────┼───────────────────┼───────────────────┘
                           │                   │
                           ▼                   ▼
                    ┌─────────────┐    ┌─────────────┐
                    │  Terminal   │    │  Suspended  │
                    └─────────────┘    └─────────────┘
```

### Activity System Architecture

Activities are the fundamental execution units within topics. Each activity represents a discrete step in the conversation flow.

#### Activity Type Hierarchy

```csharp
TopicFlowActivity (Abstract Base)
├── SimpleActivity
│   ├── MessageActivity
│   ├── DelayActivity
│   └── DumpContextActivity
├── AdaptiveCardActivity<TCard, TModel>
│   ├── FormCollectionActivity
│   ├── SurveyActivity
│   └── ConfirmationActivity
├── FlowControlActivity
│   ├── ConditionalActivity<T>
│   ├── RepeatActivity<T>
│   └── TriggerTopicActivity
└── SystemActivity
    ├── ErrorHandlingActivity
    ├── StateTransitionActivity
    └── LoggingActivity
```

#### Activity Execution Pipeline

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Created   │───▶│   Running   │───▶│Input/Wait   │───▶│  Completed  │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
       │                   │                   │                   │
       │                   ▼                   │                   │
       │            ┌─────────────┐            │                   │
       │            │   Failed    │            │                   │
       │            └─────────────┘            │                   │
       │                   │                   │                   │
       └───────────────────┼───────────────────┼───────────────────┘
                           │                   │
                           ▼                   ▼
                    ┌─────────────┐    ┌─────────────┐
                    │   Error     │    │ Validation  │
                    │  Recovery   │    │   Failed    │
                    └─────────────┘    └─────────────┘
```

## Event-Driven Architecture

ConversaCore implements a comprehensive event system for loose coupling and extensibility.

### Event System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Event Bus (Singleton)                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │Event Queue  │  │Subscription │  │     Event Router        │ │
│  │             │  │ Registry    │  │                         │ │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                               │
              ┌─────────────────┼─────────────────┐
              │                 │                 │
    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
    │Topic Events │    │Activity     │    │ UI Events   │
    │             │    │ Events      │    │             │
    └─────────────┘    └─────────────┘    └─────────────┘
              │                 │                 │
    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
    │Lifecycle    │    │Card         │    │Navigation   │
    │Changed      │    │Rendered     │    │Events       │
    └─────────────┘    └─────────────┘    └─────────────┘
```

### Event Categories

#### 1. **Topic Events**
```csharp
public enum TopicEventType
{
    TopicActivated,
    TopicCompleted,
    TopicFailed,
    TopicSuspended,
    TopicResumed
}
```

#### 2. **Activity Events**
```csharp
public enum ActivityEventType
{
    ActivityCreated,
    ActivityStarted,
    ActivityCompleted,
    ActivityFailed,
    InputCollected,
    ValidationFailed
}
```

#### 3. **Card Events**
```csharp
public enum CardEventType
{
    CardJsonEmitted,
    CardJsonSending,
    CardJsonSent,
    CardRendered,
    CardDataReceived,
    ModelBound
}
```

#### 4. **System Events**
```csharp
public enum SystemEventType
{
    ConversationStarted,
    ConversationEnded,
    ErrorOccurred,
    StateChanged,
    NavigationRequested
}
```

## State Management

ConversaCore provides a sophisticated state management system for maintaining conversation context and flow state.

### State Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   Conversation Context                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │User Profile│  │Session Data │  │    Topic Chain          │ │
│  │             │  │             │  │                         │ │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                               │
              ┌─────────────────┼─────────────────┐
              │                 │                 │
    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
    │Topic        │    │Activity     │    │ Workflow    │
    │Workflow     │    │ State       │    │ Context     │
    │Context      │    │ Machine     │    │             │
    └─────────────┘    └─────────────┘    └─────────────┘
              │                 │                 │
    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
    │Key-Value    │    │State        │    │Cross-Topic  │
    │Store        │    │Transitions  │    │Data         │
    └─────────────┘    └─────────────┘    └─────────────┘
```

### State Persistence Strategy

```csharp
public interface IConversationContext
{
    // Core identity
    string ConversationId { get; }
    string UserId { get; }
    
    // Topic flow tracking
    string CurrentTopic { get; }
    IReadOnlyList<string> TopicChain { get; }
    
    // Data storage
    T GetValue<T>(string key);
    void SetValue<T>(string key, T value);
    void RemoveValue(string key);
    
    // State management
    void SetCurrentTopic(string topicName);
    void AddTopicToChain(string topicName);
    
    // Persistence
    Task SaveStateAsync();
    Task LoadStateAsync();
}
```

### State Machine Integration

Each topic contains an integrated state machine for managing complex conversation flows:

```csharp
public class TopicStateMachine<TState> : ITopicStateMachine<TState>
    where TState : struct, Enum
{
    private readonly Dictionary<TState, HashSet<TState>> _allowedTransitions;
    private readonly Dictionary<(TState From, TState To), Func<Task>> _transitionActions;
    
    public TState CurrentState { get; private set; }
    public List<StateTransition<TState>> TransitionHistory { get; }
    
    public void ConfigureTransition(TState from, TState to, Func<Task>? action = null);
    public async Task TransitionToAsync(TState newState, string? reason = null);
    public bool CanTransitionTo(TState newState);
}
```

## Topic Flow System

### Topic Architecture

Topics are the primary organizational unit in ConversaCore, encapsulating related conversation logic and maintaining their own activity queues.

```csharp
public abstract class TopicFlow
{
    // Core properties
    public string Id { get; }
    public string Name { get; }
    public int Priority { get; }
    
    // Activity management
    protected Queue<TopicFlowActivity> ActivityQueue { get; }
    protected TopicFlowActivity? CurrentActivity { get; }
    
    // State management
    protected TopicStateMachine<FlowState> StateMachine { get; }
    protected TopicWorkflowContext WorkflowContext { get; }
    
    // Intent recognition
    public abstract Task<bool> CanHandleAsync(string message, IConversationContext context);
    
    // Activity management
    protected void Add(TopicFlowActivity activity);
    protected async Task<ActivityResult> ExecuteNextActivityAsync();
    
    // Template methods
    protected abstract void BuildActivityQueue();
    protected virtual void OnTopicStarted() { }
    protected virtual void OnTopicCompleted() { }
}
```

### Activity Queue Management

Each topic maintains a FIFO queue of activities that execute sequentially:

```
┌─────────────────────────────────────────────────────────────────┐
│                        Topic Activity Queue                    │
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────│
│  │Activity 1   │─▶│Activity 2   │─▶│Activity 3   │─▶│  ...    │
│  │(Greeting)   │  │(Form Card)  │  │(Validation) │  │         │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────│
│         │                 │                 │                 │
│         ▼                 ▼                 ▼                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│  │ Completed   │  │  Current    │  │   Pending   │            │
│  │             │  │ (Executing) │  │             │            │
│  └─────────────┘  └─────────────┘  └─────────────┘            │
└─────────────────────────────────────────────────────────────────┘
```

### Flow Control Activities

ConversaCore provides sophisticated flow control activities for complex conversation patterns:

#### RepeatActivity<T>
Enables iterative data collection with embedded continuation prompts:

```csharp
var collectBeneficiaries = RepeatActivity<AdaptiveCardActivity<BeneficiaryCard, BeneficiaryModel>>
    .UserPrompted(
        "CollectBeneficiaries",
        (id, ctx) => new AdaptiveCardActivity<BeneficiaryCard, BeneficiaryModel>(
            id, 
            ctx, 
            $"Beneficiary #{ctx.GetValue<int>("iteration_count", 1)}"
        ),
        "Would you like to add another beneficiary?",
        logger
    );
```

#### ConditionalActivity<T>
Enables branching logic based on runtime conditions:

```csharp
var ageVerification = ConditionalActivity<AdaptiveCardActivity<TCard, TModel>>.If(
    "AgeCheck",
    ctx => ctx.GetValue<int>("user_age") >= 18,
    (id, ctx) => new AdultVerificationCard(id, ctx),
    (id, ctx) => new MinorConsentCard(id, ctx)
);
```

## Adaptive Cards Integration

ConversaCore provides first-class support for Microsoft Adaptive Cards, enabling rich, interactive user interfaces.

### Card Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Adaptive Card System                         │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              Card Definition Layer                      │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Card Builder │  │Card Elements│  │     Actions     │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                │                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │               Model Binding Layer                       │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Data Models  │  │Validation   │  │  Serialization  │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                │                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │               Activity Integration                      │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │   │
│  │  │Event        │  │Lifecycle    │  │   Context       │ │   │
│  │  │Handling     │  │Management   │  │  Integration    │ │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Card Activity Pattern

```csharp
public abstract class AdaptiveCardActivity<TModel> : TopicFlowActivity, IAdaptiveCardActivity
    where TModel : class
{
    // Card lifecycle events
    public event EventHandler<CardJsonEventArgs>? CardJsonEmitted;
    public event EventHandler<CardJsonEventArgs>? CardJsonSent;
    public event EventHandler<CardJsonRenderedEventArgs>? CardJsonRendered;
    
    // Data lifecycle events  
    public event EventHandler<CardDataReceivedEventArgs>? CardDataReceived;
    public event EventHandler<ModelBoundEventArgs>? ModelBound;
    public event EventHandler<ValidationFailedEventArgs>? ValidationFailed;
    
    // Template methods
    protected abstract string GenerateCardJson(TopicWorkflowContext context);
    protected abstract TModel BindModel(Dictionary<string, object> cardData);
    protected abstract ValidationResult ValidateModel(TModel model);
    protected abstract ActivityResult ProcessValidModel(TModel model);
}
```

### BaseCardModel Pattern

All card models inherit from BaseCardModel to support embedded continuation:

```csharp
public abstract class BaseCardModel
{
    [JsonPropertyName("user_response")]
    public string? UserResponse { get; set; }
    
    // Additional common properties
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? ValidationErrors { get; set; }
}
```

### Card Enhancement System

The RepeatPromptInjector enables dynamic card enhancement for continuation workflows:

```csharp
public static class RepeatPromptInjector
{
    public static AdaptiveCard InjectRepeatPrompt(
        AdaptiveCard originalCard,
        string promptText,
        int currentCount,
        string itemType = "item")
    {
        // Create enhanced card with continuation prompt
        var enhancedCard = new AdaptiveCard("1.5");
        
        // Add completion indicator
        enhancedCard.Body.Add(new AdaptiveTextBlock
        {
            Text = $"✅ {itemType} #{currentCount} completed!",
            Size = AdaptiveTextSize.Medium,
            Weight = AdaptiveTextWeight.Bolder,
            Color = AdaptiveTextColor.Good
        });
        
        // Add continuation prompt
        enhancedCard.Body.Add(new AdaptiveChoiceSetInput
        {
            Id = "user_response",
            Choices = new List<AdaptiveChoice>
            {
                new() { Title = $"➕ Yes, add another {itemType}", Value = "continue" },
                new() { Title = $"✅ No, I'm done adding {itemType}s", Value = "stop" }
            },
            Value = "continue"
        });
        
        return enhancedCard;
    }
}
```

## Service Layer

The service layer provides high-level orchestration and integration capabilities.

### Core Services

#### ChatService
Orchestrates conversation flow and manages topic selection:

```csharp
public interface IChatService
{
    Task<ChatResponse> ProcessMessageAsync(string message, CancellationToken cancellationToken = default);
    Task<bool> CanHandleTopicAsync(string topicName, string message);
    Task ResetConversationAsync();
    Task<ConversationSummary> GetConversationSummaryAsync();
}
```

#### IntentRecognitionService
Provides intelligent topic matching based on user intent:

```csharp
public interface IIntentRecognitionService
{
    Task<IntentRecognitionResult> RecognizeIntentAsync(string message, IConversationContext context);
    Task<double> CalculateTopicConfidenceAsync(string message, string topicName);
    Task TrainIntentModelAsync(IEnumerable<TrainingExample> examples);
}
```

#### SemanticKernelService
Integrates with Microsoft Semantic Kernel for AI-powered responses:

```csharp
public interface ISemanticKernelService
{
    Task<SemanticKernelResponse> ProcessMessageAsync(string message, ChatSessionState sessionState);
    Task<SemanticKernelResponse> GenerateResponseAsync(string prompt, IConversationContext context, CancellationToken cancellationToken);
    Task<bool> ValidateResponseQualityAsync(string response);
}
```

### Service Dependencies

```
┌─────────────────────────────────────────────────────────────────┐
│                      Service Layer                             │
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐ │
│  │Chat Service │───▶│Intent Recog.│───▶│  Semantic Kernel    │ │
│  │             │    │ Service     │    │     Service         │ │
│  └─────────────┘    └─────────────┘    └─────────────────────┘ │
│         │                   │                       │          │
│         ▼                   ▼                       ▼          │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐ │
│  │Topic        │    │Conversation │    │   External APIs     │ │
│  │Registry     │    │ Context     │    │                     │ │
│  └─────────────┘    └─────────────┘    └─────────────────────┘ │
│         │                   │                       │          │
│         ▼                   ▼                       ▼          │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐ │
│  │Event Bus    │    │State        │    │   Logging &         │ │
│  │             │    │Management   │    │   Telemetry         │ │
│  └─────────────┘    └─────────────┘    └─────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Extension Points

ConversaCore provides numerous extension points for customization and enhancement.

### Custom Activities

```csharp
public abstract class CustomActivity : TopicFlowActivity
{
    protected CustomActivity(string id) : base(id) { }
    
    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        // Custom implementation
        return ActivityResult.Continue();
    }
}
```

### Custom Topics

```csharp
public class CustomTopic : TopicFlow
{
    public override async Task<bool> CanHandleAsync(string message, IConversationContext context)
    {
        // Custom intent recognition logic
        return await Task.FromResult(false);
    }
    
    protected override void BuildActivityQueue()
    {
        // Build custom activity sequence
        Add(new SimpleActivity("Welcome", ctx => "Hello!"));
        Add(new AdaptiveCardActivity<CustomCard, CustomModel>("Collect", Context));
        Add(new TriggerTopicActivity("NextTopic"));
    }
}
```

### Custom Event Handlers

```csharp
public class CustomEventHandler : ITopicEventHandler
{
    public async Task HandleAsync(TopicEvent topicEvent)
    {
        switch (topicEvent.EventType)
        {
            case TopicEventType.TopicActivated:
                // Custom activation logic
                break;
            case TopicEventType.TopicCompleted:
                // Custom completion logic
                break;
        }
    }
}
```

### Custom State Machines

```csharp
public enum CustomFlowState
{
    Initial,
    DataCollection,
    Validation,
    Processing,
    Completed,
    Failed
}

public class CustomStateMachine : TopicStateMachine<CustomFlowState>
{
    public CustomStateMachine()
    {
        ConfigureTransition(CustomFlowState.Initial, CustomFlowState.DataCollection);
        ConfigureTransition(CustomFlowState.DataCollection, CustomFlowState.Validation);
        ConfigureTransition(CustomFlowState.Validation, CustomFlowState.Processing);
        ConfigureTransition(CustomFlowState.Processing, CustomFlowState.Completed);
        
        // Error transitions
        ConfigureTransition(CustomFlowState.DataCollection, CustomFlowState.Failed);
        ConfigureTransition(CustomFlowState.Validation, CustomFlowState.Failed);
    }
}
```

## Design Patterns

ConversaCore implements several key design patterns for maintainability and extensibility.

### Factory Pattern

```csharp
// Activity Factory
public static class ActivityFactory
{
    public static RepeatActivity<T> CreateRepeatActivity<T>(
        string id,
        Func<string, TopicWorkflowContext, T> factory,
        string? prompt = null,
        ILogger? logger = null) where T : TopicFlowActivity
    {
        return RepeatActivity<T>.UserPrompted(id, factory, prompt, logger);
    }
    
    public static ConditionalActivity<T> CreateConditionalActivity<T>(
        string id,
        Func<TopicWorkflowContext, bool> condition,
        Func<string, TopicWorkflowContext, T> trueFactory,
        Func<string, TopicWorkflowContext, T> falseFactory,
        ILogger? logger = null) where T : TopicFlowActivity
    {
        return ConditionalActivity<T>.If(id, condition, trueFactory, falseFactory, logger);
    }
}
```

### Observer Pattern

```csharp
// Event-driven communication
public interface ITopicObserver
{
    Task OnTopicEventAsync(TopicEvent eventArgs);
}

public class TopicEventBus : ITopicEventBus
{
    private readonly List<ITopicObserver> _observers = new();
    
    public void Subscribe(ITopicObserver observer)
    {
        _observers.Add(observer);
    }
    
    public async Task PublishAsync(TopicEvent eventArgs)
    {
        foreach (var observer in _observers)
        {
            await observer.OnTopicEventAsync(eventArgs);
        }
    }
}
```

### Strategy Pattern

```csharp
// Intent recognition strategies
public interface IIntentRecognitionStrategy
{
    Task<double> CalculateConfidenceAsync(string message, string topicName);
}

public class KeywordStrategy : IIntentRecognitionStrategy
{
    public async Task<double> CalculateConfidenceAsync(string message, string topicName)
    {
        // Keyword-based confidence calculation
        return await Task.FromResult(0.5);
    }
}

public class SemanticStrategy : IIntentRecognitionStrategy
{
    public async Task<double> CalculateConfidenceAsync(string message, string topicName)
    {
        // Semantic similarity-based confidence calculation
        return await Task.FromResult(0.8);
    }
}
```

### Template Method Pattern

```csharp
public abstract class TopicFlowTemplate
{
    public async Task<TopicResult> ExecuteAsync()
    {
        await Initialize();
        var result = await ProcessCore();
        await Cleanup();
        return result;
    }
    
    protected virtual Task Initialize() => Task.CompletedTask;
    protected abstract Task<TopicResult> ProcessCore();
    protected virtual Task Cleanup() => Task.CompletedTask;
}
```

### Decorator Pattern

```csharp
public class LoggingTopicDecorator : ITopicFlow
{
    private readonly ITopicFlow _inner;
    private readonly ILogger _logger;
    
    public LoggingTopicDecorator(ITopicFlow inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }
    
    public async Task<bool> CanHandleAsync(string message, IConversationContext context)
    {
        _logger.LogInformation("Checking if topic {TopicName} can handle message", _inner.Name);
        return await _inner.CanHandleAsync(message, context);
    }
}
```

## Best Practices

### Topic Design Guidelines

1. **Single Responsibility**: Each topic should handle one specific conversation domain
2. **Clear Boundaries**: Define clear entry and exit conditions for topics
3. **Intent Recognition**: Implement robust `CanHandleAsync` methods with proper confidence scoring
4. **Error Handling**: Include error recovery activities in topic flows
5. **State Management**: Use TopicWorkflowContext for topic-specific data storage

### Activity Design Guidelines

1. **Atomic Operations**: Each activity should represent a single, indivisible operation
2. **Event Emission**: Emit appropriate events for lifecycle management
3. **Error Propagation**: Handle errors gracefully and provide meaningful error messages
4. **Context Preservation**: Maintain conversation context across activity transitions
5. **Testability**: Design activities to be easily unit testable

### State Management Guidelines

1. **Immutable State**: Prefer immutable state objects where possible
2. **Context Scope**: Use appropriate scope for context data (conversation vs. topic vs. activity)
3. **Cleanup**: Implement proper cleanup of temporary state data
4. **Validation**: Validate state transitions and data integrity
5. **Persistence**: Design for state persistence across application restarts

### Event System Guidelines

1. **Loose Coupling**: Use events to minimize dependencies between components
2. **Event Naming**: Use descriptive event names that clearly indicate the action
3. **Event Data**: Include sufficient context in event data for handlers
4. **Error Handling**: Implement proper error handling in event handlers
5. **Performance**: Be mindful of event handler performance impact

## Implementation Guidelines

### Setting Up a New ConversaCore Application

1. **Create Application Host**:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add ConversaCore services
builder.Services.AddConversaCore();

// Add application-specific services
builder.Services.AddScoped<ICustomService, CustomService>();

// Register custom topics
builder.Services.AddScoped<ITopic, CustomTopic>();

var app = builder.Build();
```

2. **Configure Topic Registry**:
```csharp
public static class TopicRegistration
{
    public static void AddCustomTopics(this IServiceCollection services)
    {
        services.AddScoped<ITopic>(sp => new BeneficiaryInfoTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<BeneficiaryInfoTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));
        
        services.AddScoped<ITopic>(sp => new InsuranceNeedsTopic(
            sp.GetRequiredService<TopicWorkflowContext>(),
            sp.GetRequiredService<ILogger<InsuranceNeedsTopic>>(),
            sp.GetRequiredService<IConversationContext>()
        ));
    }
}
```

3. **Implement Custom Topic**:
```csharp
public class CustomTopic : TopicFlow
{
    public CustomTopic(
        TopicWorkflowContext context,
        ILogger<CustomTopic> logger,
        IConversationContext conversationContext)
        : base("Custom", context, logger, conversationContext)
    {
        Priority = 100;
        IntentKeywords = new[] { "custom", "special", "unique" };
    }
    
    public override async Task<bool> CanHandleAsync(string message, IConversationContext context)
    {
        var normalizedMessage = message.ToLower();
        var confidence = IntentKeywords.Count(keyword => normalizedMessage.Contains(keyword)) / (double)IntentKeywords.Length;
        return await Task.FromResult(confidence > 0.5);
    }
    
    protected override void BuildActivityQueue()
    {
        Add(new SimpleActivity("Greeting", ctx => "Welcome to the custom topic!"));
        
        Add(RepeatActivity<AdaptiveCardActivity<CustomCard, CustomModel>>.UserPrompted(
            "CollectData",
            (id, ctx) => new AdaptiveCardActivity<CustomCard, CustomModel>(id, ctx),
            "Would you like to add more data?",
            Logger
        ));
        
        Add(new SimpleActivity("Summary", ctx => "Thank you for the information!"));
        Add(new TriggerTopicActivity("NextTopic"));
    }
}
```

4. **Create Custom Activity**:
```csharp
public class CustomCardActivity : AdaptiveCardActivity<CustomModel>
{
    public CustomCardActivity(string id, TopicWorkflowContext context, string? customMessage = null)
        : base(id, context, Logger, customMessage ?? "Please provide information")
    {
    }
    
    protected override string GenerateCardJson(TopicWorkflowContext context)
    {
        var card = new CustomCard();
        return card.ToJson();
    }
    
    protected override CustomModel BindModel(Dictionary<string, object> cardData)
    {
        return new CustomModel
        {
            Name = cardData.GetValueOrDefault("name")?.ToString(),
            Email = cardData.GetValueOrDefault("email")?.ToString()
        };
    }
    
    protected override ValidationResult ValidateModel(CustomModel model)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(model.Name))
            errors.Add("Name is required");
            
        if (string.IsNullOrEmpty(model.Email))
            errors.Add("Email is required");
            
        return new ValidationResult { IsValid = errors.Count == 0, Errors = errors };
    }
    
    protected override ActivityResult ProcessValidModel(CustomModel model)
    {
        Context.SetValue("custom_data", model);
        return ActivityResult.Continue("Information collected successfully");
    }
}
```

### Testing Guidelines

1. **Unit Testing Topics**:
```csharp
[Test]
public async Task CustomTopic_CanHandle_ReturnsTrue_ForRelevantMessage()
{
    // Arrange
    var topic = new CustomTopic(context, logger, conversationContext);
    var message = "I need custom assistance";
    
    // Act
    var canHandle = await topic.CanHandleAsync(message, conversationContext);
    
    // Assert
    Assert.IsTrue(canHandle);
}
```

2. **Integration Testing**:
```csharp
[Test]
public async Task ConversationFlow_CompletesTopic_Successfully()
{
    // Arrange
    var chatService = serviceProvider.GetRequiredService<IChatService>();
    
    // Act
    var response1 = await chatService.ProcessMessageAsync("Start custom flow");
    var response2 = await chatService.ProcessMessageAsync("John Doe");
    
    // Assert
    Assert.IsTrue(response1.UsedTopicSystem);
    Assert.Contains("custom", response1.Content.ToLower());
}
```

### Performance Considerations

1. **Activity Execution**: Keep activity execution lightweight and avoid blocking operations
2. **Event Handling**: Minimize event handler execution time to prevent bottlenecks
3. **State Storage**: Use appropriate storage mechanisms for conversation state based on scale requirements
4. **Memory Management**: Implement proper disposal patterns for long-running conversations
5. **Caching**: Cache frequently accessed topic and activity metadata

### Security Considerations

1. **Input Validation**: Validate all user inputs in activity implementations
2. **State Protection**: Protect sensitive data in conversation context
3. **Event Security**: Validate event data to prevent injection attacks
4. **Access Control**: Implement proper access control for administrative features
5. **Data Privacy**: Implement data retention and deletion policies for conversation data

---

## Conclusion

ConversaCore provides a robust, extensible framework for building sophisticated conversational AI applications. By following the architectural patterns and best practices outlined in this document, developers can create maintainable, scalable conversation systems that provide rich user experiences while maintaining clean separation of concerns and extensibility.

The framework's event-driven architecture, combined with its comprehensive activity system and adaptive card integration, enables the creation of complex conversation flows that can handle real-world business scenarios effectively.

For questions or contributions to this documentation, please refer to the project repository and contribution guidelines.