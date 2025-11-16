using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
public sealed class ConversationStartTopic : ConversaCore.TopicFlow.TopicFlow {
    // Constants for activity IDs and context keys
    public const string GreetActivityId = "greet";
    private const string InitIntent = "__init__";
    private const string ContextFlag = "ConversationStartTopic.HasRun";

    private readonly IConversationContext _conversationContext;
    private readonly ILogger<ConversationStartTopic> _logger;
    private bool _initialized;

    // -------------------------------------------------------------
    // Constructor: minimal logic only (deferred initialization)
    // -------------------------------------------------------------
    public ConversationStartTopic(
        TopicWorkflowContext context,
        ILogger<ConversationStartTopic> logger,
        IConversationContext conversationContext)
        : base(context, logger) {
        _conversationContext = conversationContext;
        _logger = logger;
        _initialized = false;

        Console.WriteLine($"[DI] ▶ Entering ConversationStartTopic ctor ({GetHashCode()})");
        // ❗ Avoid any Context.SetValue(), AddActivity(), or reflection here.
        Console.WriteLine($"[DI] ◀ Exiting ConversationStartTopic ctor ({GetHashCode()})");
    }

    // -------------------------------------------------------------
    // Lazy runtime initialization (runs only once)
    // -------------------------------------------------------------
    private void EnsureInitialized() {
        if (_initialized)
            return;

        _initialized = true;
        Console.WriteLine($"[ConversationStartTopic] 🔁 Initializing runtime context...");

        // Initialize conversation metadata
        _conversationContext.SetValue("ConversationStartTopic_create", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("ConversationId", Guid.NewGuid().ToString());
        _conversationContext.SetValue("ConversationStartTime", DateTime.UtcNow);

        // Initialize global structure
        InitializeGenericGlobalStructure();

        // Add greeting activity
        Add(new GreetingActivity(GreetActivityId));

        _logger.LogInformation("[ConversationStartTopic] Initialized with GreetActivity only (system topic)");
        _logger.LogInformation("[ConversationStartTopic] Initialized generic global structure");

        // Mark as initialized
        Context.SetValue(ContextFlag, true);
    }

    // -------------------------------------------------------------
    // Topic reset
    // -------------------------------------------------------------
    public override void Reset() {
        Context.SetValue(ContextFlag, false);
        base.Reset();

        // Reset internal FSM state safely
        var stateMachineField = typeof(ConversaCore.TopicFlow.TopicFlow)
            .GetField("_fsm", BindingFlags.NonPublic | BindingFlags.Instance);
        var stateMachine = stateMachineField?.GetValue(this);

        if (stateMachine is ConversaCore.StateMachine.ITopicStateMachine<ConversaCore.TopicFlow.TopicFlow.FlowState> fsm)
            fsm.ForceState(ConversaCore.TopicFlow.TopicFlow.FlowState.Idle, "Forced reset to Idle in ConversationStartTopic.Reset");

        // Reset conversation metadata
        _conversationContext.SetValue("ConversationStartTopic_create", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("ConversationId", Guid.NewGuid().ToString());
        _conversationContext.SetValue("ConversationStartTime", DateTime.UtcNow);
        _conversationContext.SetValue("ConversationLastReset", DateTime.UtcNow.ToString("o"));

        // Rebuild generic globals
        InitializeGenericGlobalStructure();

        _conversationContext.SetValue("ConversationStartTopic.HasRun", false);
        _conversationContext.SetValue("TopicsInitialized", false);

        Context.SetValue(ContextFlag, true);
    }

    // -------------------------------------------------------------
    // Initialize global variables
    // -------------------------------------------------------------
    private void InitializeGenericGlobalStructure() {
        _conversationContext.SetValue("Global_ConversationActive", true);
        _conversationContext.SetValue("Global_TopicHistory", new List<string>());
        _conversationContext.SetValue("Global_UserInteractionCount", 0);
        _conversationContext.SetValue("Global_GenericStructureInitialized", true);
    }

    // -------------------------------------------------------------
    // Core metadata
    // -------------------------------------------------------------
    public override string Name => "ConversationStart";
    public override int Priority => int.MaxValue;

    // -------------------------------------------------------------
    // Topic runtime execution
    // -------------------------------------------------------------
    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        EnsureInitialized();

        _conversationContext.SetValue("ConversationStartTopic_runasync", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("TopicName", "Conversation Start");
        _conversationContext.SetCurrentTopic(Name);

        UpdateNextTopicFromContext();
        return base.RunAsync(cancellationToken);
    }

    // -------------------------------------------------------------
    // Update trigger activity (reflective)
    // -------------------------------------------------------------
    private void UpdateNextTopicFromContext() {
        var activities = GetType()
            .BaseType?
            .GetField("_activities", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(this) as List<TopicFlowActivity>;

        if (activities == null) {
            _logger.LogWarning("[ConversationStartTopic] Could not access activities collection through reflection");
            return;
        }

        var triggerActivity = activities.FirstOrDefault(a => a.Id == "TriggerNextTopic") as TriggerTopicActivity;
        if (triggerActivity != null) {
            string nextTopicName = Context.GetValue<string>("next_topic") ?? "FallbackTopic";
            if (triggerActivity.TopicToTrigger != nextTopicName) {
                _logger.LogInformation("[ConversationStartTopic] Updating next topic from {Old} to {New}",
                    triggerActivity.TopicToTrigger, nextTopicName);

                var updatedTrigger = new TriggerTopicActivity(
                    "TriggerNextTopic",
                    nextTopicName,
                    _logger,
                    waitForCompletion: false,
                    _conversationContext);

                int index = activities.IndexOf(triggerActivity);
                if (index >= 0) {
                    activities[index] = updatedTrigger;
                    _logger.LogInformation("[ConversationStartTopic] Successfully updated trigger activity");
                }
            }
        }
    }

    // -------------------------------------------------------------
    // Eligibility to handle message
    // -------------------------------------------------------------
    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        var hasRun = Context.GetValue<bool>(ContextFlag, false);
        return Task.FromResult((message == InitIntent && !hasRun) ? 1.0f : 0.0f);
    }

    // -------------------------------------------------------------
    // Process incoming message
    // -------------------------------------------------------------
    public override async Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        EnsureInitialized();

        _conversationContext.SetValue(ContextFlag, true);
        _conversationContext.AddTopicToHistory(Name);
        return await RunAsync(cancellationToken);
    }
}

/// <summary>
/// Generic card type for dynamic type resolution.
/// </summary>
public class GenericCard { }
