using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Context;
using ConversaCore.Cards;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

namespace ConversaCore.SystemTopics;

/// <summary>
/// Kicks off the conversation with a friendly welcome and handles the compliance flowchart.
/// Implements complex branching logic based on TCPA consent, CCPA acknowledgment, and CA residency.
/// Highest priority topic.
/// </summary>
public sealed class ConversationStartTopic : ConversaCore.TopicFlow.TopicFlow
{
    public override void Reset() 
    {
        // Clear initialization flag
        Context.SetValue(ContextFlag, false);

        // Call base reset which will reset activities and FSM
        base.Reset();
        
        // Double-check the state machine is truly reset to Idle
        // Use reflection to safely access the protected _fsm field
        var stateMachine = typeof(ConversaCore.TopicFlow.TopicFlow).GetField("_fsm", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance)?.GetValue(this);
        
        if (stateMachine is ConversaCore.StateMachine.ITopicStateMachine<ConversaCore.TopicFlow.TopicFlow.FlowState> fsm) {
            fsm.ForceState(ConversaCore.TopicFlow.TopicFlow.FlowState.Idle, "Forced reset to Idle in ConversationStartTopic.Reset");
        }
        
        // Re-initialize activities with the complete compliance flowchart
        //InitializeComplianceFlowchart();
        
        // Update timestamps and generate new conversation ID
        _conversationContext.SetValue("ConversationStartTopic_create", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("ConversationId", Guid.NewGuid().ToString());
        _conversationContext.SetValue("ConversationStartTime", DateTime.UtcNow);
        _conversationContext.SetValue("ConversationLastReset", DateTime.UtcNow.ToString("o"));
        
        // Re-initialize the global structure
        InitializeGenericGlobalStructure();
        
        // Make sure this isn't marked as "already processed" in both contexts
        _conversationContext.SetValue("ConversationStartTopic.HasRun", false);
        _conversationContext.SetValue("TopicsInitialized", false);
        
        // Mark this topic as initialized with the context flag
        Context.SetValue(ContextFlag, true);
    }

    // Constants for activity IDs
    public const string GreetActivityId = "greet";
    private const string InitIntent = "__init__";
    private const string ContextFlag = "ConversationStartTopic.HasRun";
    
    // Activity IDs for the compliance flowchart
    private const string ActivityId_CollectCompliance = "CollectCompliance";
    private const string ActivityId_TcpaCheck = "TcpaConsentCheck";
    private const string ActivityId_CcpaCheck = "CcpaAckCheck";
    private const string ActivityId_CaliforniaCheck = "CaliforniaCheck";
    private const string ActivityId_CollectCaliforniaInfo = "CollectCaliforniaInfo";
    private const string ActivityId_ShowHighIntentCard = "ShowHighIntentCard";
    private const string ActivityId_ShowMediumIntentCard = "ShowMediumIntentCard";
    private const string ActivityId_ShowLowIntentCard = "ShowLowIntentCard";
    private const string ActivityId_ShowBlockedCard = "ShowBlockedCard";
    private const string ActivityId_RouteToNextTopic = "RouteToNextTopic";

    private readonly IConversationContext _conversationContext;
    private readonly ILogger<ConversationStartTopic> _logger;

    public ConversationStartTopic(
        ConversaCore.TopicFlow.TopicWorkflowContext context, 
        ILogger<ConversationStartTopic> logger,
        IConversationContext conversationContext)
        : base(context, logger) 
    {
    // >>> other than the GreetActivity all the other activities added here are domain-specific and belong in the insuranceagent
    _conversationContext = conversationContext;
    _logger = logger;

    // Initialize generic conversation metadata
    _conversationContext.SetValue("ConversationStartTopic_create", DateTime.UtcNow.ToString("o"));
    _conversationContext.SetValue("ConversationId", Guid.NewGuid().ToString());
    _conversationContext.SetValue("ConversationStartTime", DateTime.UtcNow);

    // Initialize basic global variable structure
    InitializeGenericGlobalStructure();

    // Only add GreetActivity here. All other activities are domain-specific and should be added in the insurance agent layer.
    Add(new GreetingActivity(GreetActivityId));

    logger.LogInformation("[ConversationStartTopic] Initialized with GreetActivity only (system topic)");
    logger.LogInformation("[ConversationStartTopic] Initialized generic global structure");

    // Mark this topic as initialized
    Context.SetValue(ContextFlag, true);
    }

    // ...existing code...

    /// <summary>
    /// Creates the appropriate outcome card activity based on the decision matrix result
    /// </summary>
    private AdaptiveCardActivity<GenericCard, BaseCardModel> CreateOutcomeCardActivity(
        string id, 
        ConversaCore.TopicFlow.TopicWorkflowContext context, 
        string cardType)
    {
        // Create a generic activity for the scenario outcome card
        var activity = new AdaptiveCardActivity<GenericCard, BaseCardModel>(
            id,
            context,
            card => {
                // Using reflection to create the appropriate card type dynamically
                Type? cardTypeClass = Type.GetType($"InsuranceAgent.Topics.ComplianceTopic.{cardType}, InsuranceAgent");
                if (cardTypeClass == null) {
                    _logger.LogError("[ConversationStartTopic] Failed to find card type: {CardType}", cardType);
                    throw new InvalidOperationException($"Could not find card type {cardType}");
                }
                
                var cardInstance = Activator.CreateInstance(cardTypeClass);
                if (cardInstance == null) {
                    _logger.LogError("[ConversationStartTopic] Failed to create card instance: {CardType}", cardType);
                    throw new InvalidOperationException($"Could not create card instance for {cardType}");
                }
                
                var createMethod = cardTypeClass.GetMethod("Create");
                if (createMethod == null) {
                    _logger.LogError("[ConversationStartTopic] Failed to find Create method on card: {CardType}", cardType);
                    throw new InvalidOperationException($"Could not find Create method on card {cardType}");
                }
                
                var result = createMethod.Invoke(cardInstance, null);
                if (result == null)
                {
                    _logger.LogError("[ConversationStartTopic] Create method returned null for card: {CardType}", cardType);
                    throw new InvalidOperationException($"Create method returned null for card {cardType}");
                }
                return (AdaptiveCardModel)result;
            },
            "OutcomeModel"
        );
        
        // Hook up events for logging
        activity.CardJsonEmitted += (s, e) => 
            _logger.LogInformation("[{Topic}] {CardType} emitted (mode={Mode})", Name, cardType, e.RenderMode);
            
        activity.ModelBound += (s, e) => 
            _logger.LogInformation("[{Topic}] {CardType} model bound", Name, cardType);
            
        return activity;
    }

    /// <summary>
    /// Initializes the generic global variable structure.
    /// Domain-specific initialization should be handled by the consuming application.
    /// Follows Copilot Studio pattern of declaring global variables upfront.
    /// </summary>
    private void InitializeGenericGlobalStructure()
    {
        // Initialize basic conversation-level metadata
        _conversationContext.SetValue("Global_ConversationActive", true);
        _conversationContext.SetValue("Global_TopicHistory", new List<string>());
        _conversationContext.SetValue("Global_UserInteractionCount", 0);
        
        // Note: Domain-specific models and variables should be initialized
        // by the consuming application (e.g., HybridChatService for insurance domain)
        
        _conversationContext.SetValue("Global_GenericStructureInitialized", true);
    }

    public override string Name => "ConversationStart";
    public override int Priority => int.MaxValue;

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) 
    {
        _conversationContext.SetValue("ConversationStartTopic_runasync", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("TopicName", "Conversation Start");
        _conversationContext.SetCurrentTopic(Name);
        
        // Update the next topic in the trigger activity if it's changed
        // This ensures we're using the most current context values
        UpdateNextTopicFromContext();
        
        return base.RunAsync(cancellationToken);
    }
    
    /// <summary>
    /// Updates the next topic in the trigger activity based on the current context values
    /// </summary>
    private void UpdateNextTopicFromContext()
    {
        // Get the current activity queue
        var activities = GetType()
            .BaseType?
            .GetField("_activities", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(this) as List<TopicFlowActivity>;

        if (activities == null) 
        {
            _logger.LogWarning("[ConversationStartTopic] Could not access activities collection through reflection");
            return;
        }

        // Find the trigger activity
        var triggerActivity = activities.FirstOrDefault(a => a.Id == "TriggerNextTopic") as TriggerTopicActivity;
        if (triggerActivity != null)
        {
            // Update its topic name from context
            string nextTopicName = Context.GetValue<string>("next_topic") ?? "FallbackTopic";
            if (triggerActivity.TopicToTrigger != nextTopicName)
            {
                _logger.LogInformation("[ConversationStartTopic] Updating next topic from {Old} to {New}", 
                    triggerActivity.TopicToTrigger, nextTopicName);
                
                // Create a new trigger activity with the updated topic name
                var updatedTriggerActivity = new TriggerTopicActivity(
                    "TriggerNextTopic",
                    nextTopicName,
                    _logger,
                    waitForCompletion: false,
                    _conversationContext
                );
                
                // Replace the activity in the queue
                int index = activities.IndexOf(triggerActivity);
                if (index >= 0)
                {
                    activities[index] = updatedTriggerActivity;
                    _logger.LogInformation("[ConversationStartTopic] Successfully updated trigger activity");
                }
            }
        }
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        // Only match the special initialization intent and only if not already run
        var hasRun = Context.GetValue<bool>(ContextFlag, false);
        return Task.FromResult((message == InitIntent && !hasRun) ? 1.0f : 0.0f);
    }

    public override async Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        // Mark as run so it cannot be triggered again
        _conversationContext.SetValue(ContextFlag, true);
        _conversationContext.AddTopicToHistory(Name);
        return await RunAsync(cancellationToken);
    }
}

/// <summary>
/// Generic card type for dynamic type resolution
/// </summary>
public class GenericCard { }
