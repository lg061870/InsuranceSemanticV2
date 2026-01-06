# V3 Event-Driven Architecture Refactoring Guide

## ?? **Overview**

This guide provides step-by-step instructions for refactoring the V3 chat window architecture from direct method calls to an event-driven pattern. This improves decoupling, testability, and enforces proper domain implementation patterns.

---

## ?? **Goals**

1. ? **Make DomainAgentService abstract** with required event handlers
2. ? **Make core methods protected** (only callable via event handlers)
3. ? **Add UI events to CustomChatWindowV3** (raised instead of direct calls)
4. ? **Implement event handlers in InsuranceAgentServiceV2**
5. ? **Force domain developers to implement handlers** (compiler enforced)

---

## ??? **Architecture Before & After**

### **Before (V3 - Direct Calls):**
```
CustomChatWindowV3
    ? (Direct method calls)
InsuranceAgentServiceV2.StartConversationPublicAsync()
InsuranceAgentServiceV2.ProcessUserMessageAsync()
InsuranceAgentServiceV2.HandleCardSubmitAsync()
InsuranceAgentServiceV2.ResetConversationAsync()
```

### **After (V3 - Event-Driven):**
```
CustomChatWindowV3
    ? (Raises events)
ConversationStartRequested event
UserMessageReceived event
CardSubmitted event
ConversationResetRequested event
    ? (Event handlers)
InsuranceAgentServiceV2.OnConversationStartRequestedAsync()
InsuranceAgentServiceV2.OnUserMessageReceivedAsync()
InsuranceAgentServiceV2.OnCardSubmittedAsync()
InsuranceAgentServiceV2.OnConversationResetRequestedAsync()
    ? (Calls protected methods)
DomainAgentService.StartConversationAsync()
DomainAgentService.ProcessUserMessageAsync()
DomainAgentService.HandleCardSubmitAsync()
DomainAgentService.ResetConversationAsync()
```

---

## ?? **Implementation Steps**

### **Step 1: Event Args Classes Already Created ?**

**File:** `ConversaCore/Events/ChatWindowEventArgs.cs`

This file was already created with:
- `ConversationStartRequestedEventArgs`
- `UserMessageReceivedEventArgs`
- `CardSubmittedEventArgs`
- `ConversationResetRequestedEventArgs`

---

### **Step 2: Update DomainAgentService (Make Abstract)**

**File:** `ConversaCore/Agentic/DomainAgentService.cs`

#### **2.1: Change class declaration to abstract**

```csharp
// OLD:
public class DomainAgentService {

// NEW:
public abstract class DomainAgentService {
```

#### **2.2: Add abstract event handler methods (AFTER constructor, BEFORE existing methods)**

```csharp
public abstract class DomainAgentService {

    // ...existing fields and constructor...

    // ========================================================================
    // ABSTRACT EVENT HANDLERS - MUST be implemented by domain services
    // ========================================================================
    
    /// <summary>
    /// Handles conversation start request from UI.
    /// Domain implementations must override to provide startup logic.
    /// </summary>
    protected abstract Task OnConversationStartRequestedAsync(CancellationToken ct);
    
    /// <summary>
    /// Handles user message received from UI.
    /// Domain implementations must override to process user input.
    /// </summary>
    protected abstract Task OnUserMessageReceivedAsync(string message, CancellationToken ct);
    
    /// <summary>
    /// Handles adaptive card submission from UI.
    /// Domain implementations must override to process card data.
    /// </summary>
    protected abstract Task OnCardSubmittedAsync(Dictionary<string, object> data, CancellationToken ct);
    
    /// <summary>
    /// Handles conversation reset request from UI.
    /// Domain implementations must override to reset conversation state.
    /// </summary>
    protected abstract Task OnConversationResetRequestedAsync(CancellationToken ct);

    // ...existing methods follow...
```

#### **2.3: Change public methods to protected**

**Find these methods and change their access modifiers:**

```csharp
// OLD:
public async Task ProcessUserMessageAsync(string userMessage, CancellationToken ct = default)

// NEW:
protected async Task ProcessUserMessageAsync(string userMessage, CancellationToken ct = default)
```

```csharp
// OLD:
public async Task ResetConversationAsync(CancellationToken ct = default)

// NEW:
protected async Task ResetConversationAsync(CancellationToken ct = default)
```

```csharp
// OLD (in Event Handlers region):
public async Task HandleCardSubmitAsync(Dictionary<string, object> data, CancellationToken ct)

// NEW:
protected async Task HandleCardSubmitAsync(Dictionary<string, object> data, CancellationToken ct)
```

#### **2.4: Mark StartConversationPublicAsync as obsolete**

```csharp
/// <summary>
/// DEPRECATED: Use event-driven pattern via OnConversationStartRequestedAsync instead.
/// Kept for backward compatibility only.
/// </summary>
[Obsolete("Use event-driven pattern via OnConversationStartRequestedAsync instead")]
public virtual Task StartConversationPublicAsync(CancellationToken ct = default)
    => StartConversationAsync(ct);
```

---

### **Step 3: Update CustomChatWindowV3 (Add Events)**

**File:** `InsuranceAgent/Pages/Components/CustomChatWindowV3.razor`

#### **3.1: Add public events (in @code block, at the top)**

```csharp
@code {
    // ========== PUBLIC EVENTS (UI ? Agent) ==========
    
    /// <summary>
    /// Raised when the UI requests conversation start.
    /// Agent service subscribes to this event.
    /// </summary>
    public event EventHandler<ConversationStartRequestedEventArgs>? ConversationStartRequested;
    
    /// <summary>
    /// Raised when user sends a message.
    /// Agent service subscribes to this event.
    /// </summary>
    public event EventHandler<UserMessageReceivedEventArgs>? UserMessageReceived;
    
    /// <summary>
    /// Raised when user submits an adaptive card.
    /// Agent service subscribes to this event.
    /// </summary>
    public event EventHandler<CardSubmittedEventArgs>? CardSubmitted;
    
    /// <summary>
    /// Raised when user requests conversation reset.
    /// Agent service subscribes to this event.
    /// </summary>
    public event EventHandler<ConversationResetRequestedEventArgs>? ConversationResetRequested;

    // ...existing state fields...
```

#### **3.2: Replace direct method calls with event raises**

**Find and replace these calls:**

**A. In `OnAfterRenderAsync` (conversation start):**

```csharp
// OLD:
_ = AgentService.StartConversationPublicAsync(CancellationToken.None);

// NEW:
Logger?.LogInformation("[ChatWindowV3] Raising ConversationStartRequested event");
ConversationStartRequested?.Invoke(this, new ConversationStartRequestedEventArgs(CancellationToken.None));
```

**B. In `SendMessage` method:**

```csharp
// OLD:
await AgentService.ProcessUserMessageAsync(userMessageText, CancellationToken.None);

// NEW:
Logger?.LogInformation("[ChatWindowV3] Raising UserMessageReceived event: {Message}", userMessageText);
UserMessageReceived?.Invoke(this, new UserMessageReceivedEventArgs(userMessageText, CancellationToken.None));
```

**C. In `OnAdaptiveCardSubmit` method:**

```csharp
// OLD:
private Task OnAdaptiveCardSubmit(Dictionary<string, object> data) {
    Logger?.LogInformation("[ChatWindowV3] OnAdaptiveCardSubmit: {Keys}", string.Join(",", data.Keys));
    return AgentService.HandleCardSubmitAsync(data, CancellationToken.None);
}

// NEW:
private Task OnAdaptiveCardSubmit(Dictionary<string, object> data) {
    Logger?.LogInformation("[ChatWindowV3] OnAdaptiveCardSubmit: {Keys}", string.Join(",", data.Keys));
    Logger?.LogInformation("[ChatWindowV3] Raising CardSubmitted event");
    CardSubmitted?.Invoke(this, new CardSubmittedEventArgs(data, CancellationToken.None));
    return Task.CompletedTask;
}
```

**D. In `ExecuteReset` method:**

```csharp
// OLD:
await AgentService.ResetConversationAsync();

// NEW:
Logger?.LogInformation("[ChatWindowV3] Raising ConversationResetRequested event");
ConversationResetRequested?.Invoke(this, new ConversationResetRequestedEventArgs(CancellationToken.None));
```

---

### **Step 4: Update InsuranceAgentServiceV2 (Implement Handlers)**

**File:** `InsuranceAgent/Services/InsuranceAgentServiceV2.cs`

#### **4.1: Implement abstract event handlers**

Add these methods to `InsuranceAgentServiceV2`:

```csharp
#region Abstract Event Handler Implementations

/// <summary>
/// Handles conversation start request from UI.
/// </summary>
protected override async Task OnConversationStartRequestedAsync(CancellationToken ct)
{
    _logger.LogInformation("[InsuranceAgentServiceV2] Conversation start requested via event");
    await StartConversationAsync(ct);
}

/// <summary>
/// Handles user message received from UI.
/// </summary>
protected override async Task OnUserMessageReceivedAsync(string message, CancellationToken ct)
{
    _logger.LogInformation("[InsuranceAgentServiceV2] User message received via event: {Message}", message);
    await ProcessUserMessageAsync(message, ct);
}

/// <summary>
/// Handles adaptive card submission from UI.
/// </summary>
protected override async Task OnCardSubmittedAsync(Dictionary<string, object> data, CancellationToken ct)
{
    _logger.LogInformation("[InsuranceAgentServiceV2] Card submitted via event with {Count} fields", data.Count);
    await HandleCardSubmitAsync(data, ct);
}

/// <summary>
/// Handles conversation reset request from UI.
/// </summary>
protected override async Task OnConversationResetRequestedAsync(CancellationToken ct)
{
    _logger.LogInformation("[InsuranceAgentServiceV2] Conversation reset requested via event");
    await ResetConversationAsync(ct);
}

#endregion
```

#### **4.2: Subscribe to UI events in constructor**

**Update the constructor to accept `CustomChatWindowV3` and subscribe to events:**

```csharp
public InsuranceAgentServiceV2(
    TopicRegistry topicRegistry,
    IConversationContext context,
    TopicWorkflowContext wfContext,
    ILogger<InsuranceAgentServiceV2> logger)
    : base(topicRegistry, context, wfContext, logger)
{
    _logger = logger;
    _logger.LogInformation("[InsuranceAgentServiceV2] Constructor: Initializing domain-specific logic");
    
    // Initialize domain-specific globals
    InitializeInsuranceDomainGlobals();
    
    _logger.LogInformation("[InsuranceAgentServiceV2] ? Constructor complete - insurance domain initialized");
}

/// <summary>
/// Wires up event subscriptions between UI and agent service.
/// MUST be called after both ChatWindow and AgentService are created.
/// </summary>
public void SubscribeToChatWindowEvents(CustomChatWindowV3 chatWindow)
{
    _logger.LogInformation("[InsuranceAgentServiceV2] Subscribing to CustomChatWindowV3 events");
    
    // Subscribe to UI events
    chatWindow.ConversationStartRequested += async (s, e) => {
        _logger.LogInformation("[InsuranceAgentServiceV2] Event received: ConversationStartRequested");
        await OnConversationStartRequestedAsync(e.CancellationToken);
    };
    
    chatWindow.UserMessageReceived += async (s, e) => {
        _logger.LogInformation("[InsuranceAgentServiceV2] Event received: UserMessageReceived");
        await OnUserMessageReceivedAsync(e.Message, e.CancellationToken);
    };
    
    chatWindow.CardSubmitted += async (s, e) => {
        _logger.LogInformation("[InsuranceAgentServiceV2] Event received: CardSubmitted");
        await OnCardSubmittedAsync(e.Data, e.CancellationToken);
    };
    
    chatWindow.ConversationResetRequested += async (s, e) => {
        _logger.LogInformation("[InsuranceAgentServiceV2] Event received: ConversationResetRequested");
        await OnConversationResetRequestedAsync(e.CancellationToken);
    };
    
    _logger.LogInformation("[InsuranceAgentServiceV2] ? Event subscriptions complete");
}
```

---

### **Step 5: Update CustomChatWindowV3 (Wire Events)**

**File:** `InsuranceAgent/Pages/Components/CustomChatWindowV3.razor`

#### **5.1: Call subscription method after initialization**

Update `OnAfterRenderAsync`:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender) {
    if (firstRender && !_conversationStarted) {
        Logger?.LogInformation("[ChatWindowV3] ? First render - initializing event-driven architecture");
        
        // ========== SUBSCRIBE AGENT TO UI EVENTS ==========
        Logger?.LogInformation("[ChatWindowV3] Wiring agent event subscriptions");
        AgentService.SubscribeToChatWindowEvents(this);
        
        // ========== SUBSCRIBE UI TO AGENT EVENTS ==========
        Logger?.LogInformation("[ChatWindowV3] Subscribing to agent outbound events");
        
        // Bot messages
        AgentService.ActivityMessageReady += (s, e) => {
            // ...existing code...
        };

        // ...rest of existing subscriptions...

        // ========== START CONVERSATION ==========
        Logger?.LogInformation("[ChatWindowV3] Raising ConversationStartRequested event");
        ConversationStartRequested?.Invoke(this, new ConversationStartRequestedEventArgs(CancellationToken.None));

        _conversationStarted = true;
        Logger?.LogInformation("[ChatWindowV3] ? Event-driven initialization complete");
    }
}
```

---

### **Step 6: Update Program.cs (DI Registration)**

**File:** `InsuranceAgent/Program.cs`

No changes needed! The current DI registration already works because:
- `InsuranceAgentServiceV2` is registered as scoped
- `CustomChatWindowV3` will call `AgentService.SubscribeToChatWindowEvents(this)` on first render
- Both services are created by DI and event wiring happens at runtime

---

## ?? **Testing Checklist**

After implementation, verify:

- [ ] **Build succeeds** (no compilation errors)
- [ ] **App starts** without exceptions
- [ ] **Conversation starts** on load (via event)
- [ ] **User messages work** (via event)
- [ ] **Card submission works** (via event)
- [ ] **Reset button works** (via event)
- [ ] **Logs show event flow**:
  ```
  [ChatWindowV3] Raising ConversationStartRequested event
  [InsuranceAgentServiceV2] Event received: ConversationStartRequested
  [InsuranceAgentServiceV2] Conversation start requested via event
  ```

---

## ?? **Benefits of This Architecture**

1. ? **Decoupled UI** - ChatWindow doesn't depend on agent methods
2. ? **Compiler enforcement** - Domain developers MUST implement handlers
3. ? **Testable** - Events can be raised independently in tests
4. ? **Flexible** - Multiple services can subscribe to same events
5. ? **Clear separation** - UI ? Events ? Handlers ? Methods
6. ? **Protected methods** - Can't be called directly from UI

---

## ?? **Event Flow Diagram**

```
???????????????????????????
?  CustomChatWindowV3     ?
?  (UI Component)         ?
???????????????????????????
            ? Raises
            ?? ConversationStartRequested
            ?? UserMessageReceived
            ?? CardSubmitted
            ?? ConversationResetRequested
            ?
            ? Event handlers subscribe
?????????????????????????????????????
?  InsuranceAgentServiceV2          ?
?  (Domain Implementation)          ?
?                                   ?
?  OnConversationStartRequestedAsync?
?  OnUserMessageReceivedAsync       ?
?  OnCardSubmittedAsync             ?
?  OnConversationResetRequestedAsync?
?????????????????????????????????????
            ? Calls protected methods
            ?
?????????????????????????????????????
?  DomainAgentService               ?
?  (Base Class - Abstract)          ?
?                                   ?
?  StartConversationAsync()         ?
?  ProcessUserMessageAsync()        ?
?  HandleCardSubmitAsync()          ?
?  ResetConversationAsync()         ?
?????????????????????????????????????
```

---

## ?? **Common Pitfalls**

1. **? Forgetting to call `SubscribeToChatWindowEvents(this)`**
   - Events won't be handled
   - Solution: Add call in `OnAfterRenderAsync`

2. **? Making methods public instead of protected**
   - Defeats the purpose of event-driven design
   - Solution: Keep methods `protected`

3. **? Not implementing abstract handlers**
   - Compiler error
   - Solution: Implement all 4 abstract methods

4. **? Calling protected methods directly from UI**
   - Breaks encapsulation
   - Solution: Always raise events instead

---

## ?? **Related Files**

- Event Args: `ConversaCore/Events/ChatWindowEventArgs.cs`
- Base Class: `ConversaCore/Agentic/DomainAgentService.cs`
- Domain Impl: `InsuranceAgent/Services/InsuranceAgentServiceV2.cs`
- UI Component: `InsuranceAgent/Pages/Components/CustomChatWindowV3.razor`
- DI Registration: `InsuranceAgent/Program.cs`

---

## ? **Completion Checklist**

- [ ] Step 1: Event args created ? (already done)
- [ ] Step 2: DomainAgentService made abstract
- [ ] Step 3: CustomChatWindowV3 events added
- [ ] Step 4: InsuranceAgentServiceV2 handlers implemented
- [ ] Step 5: Event wiring in OnAfterRenderAsync
- [ ] Step 6: Program.cs validated (no changes needed)
- [ ] Testing: All features work
- [ ] Logs: Event flow visible

---

**Last Updated:** 2025-01-XX  
**Version:** V3 Event-Driven Refactoring  
**Status:** Implementation Guide
