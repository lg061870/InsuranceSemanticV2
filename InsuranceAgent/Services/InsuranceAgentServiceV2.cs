using ConversaCore.Cards;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using ConversaCore.Agentic;
using CoreContextExtensions = ConversaCore.TopicFlow.Core.TopicWorkflowContextExtensions;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics;

namespace InsuranceAgent.Services;

public class InsuranceAgentServiceV2 : DomainAgentService  { 

    // Activity IDs for the compliance flowchart
    private const string ActivityId_CollectCompliance = "CollectCompliance";
    private const string ActivityId_ProcessComplianceData = "ProcessComplianceData";

    public InsuranceAgentServiceV2(
        TopicRegistry topicRegistry,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<DomainAgentService> logger) : base(
        topicRegistry,
        context,
        wfContext,
        logger) {
        
        // ✅ Setup async activity handling for background semantic queries
        SetupAsyncActivityHandling();
    }

    #region Domain Agent Plumbing

    private void AddDomainActivitiesToStartTopic(TopicFlow flow) {
        LogInfo("0010006");

        flow.Add(new GreetingActivity("Greet"));

        flow.Add(new TriggerTopicActivity(ActivityId_CollectCompliance, "ComplianceTopic", _logger, waitForCompletion: true));

        flow.Add(new SimpleActivity(ActivityId_ProcessComplianceData, (ctx, input) => {
            _logger.LogInformation("[InsuranceAgentService] Processing compliance data before decision tree");
            return Task.FromResult<object?>(null);
        }));

        flow.AddRange(ComplianceFlowActivities() ?? new List<TopicFlowActivity>());

        LogInfo("0010007");
    }

    protected override async Task StartConversationAsync(CancellationToken ct = default) {
        _pausedTopics.Clear();

        var topic = _topicRegistry.GetTopic("ConversationStart");

        if (topic == null) {
            LogWarn("0010008");
            return;
        }

        if (topic is TopicFlow flow) {
            // Remove default GreetingActivity added by ConversationStartTopic.EnsureInitialized()
            flow.RemoveActivity("greet");
            flow.RemoveActivity(ActivityId_CollectCompliance);
            flow.RemoveActivity("ProcessComplianceData");
            flow.RemoveActivity("TCPAConsentSwitch");

            AddDomainActivitiesToStartTopic(flow);

            if (_activeTopic is TopicFlow currentActiveTopic)
                UnhookTopicEvents(currentActiveTopic);

            _activeTopic = flow;
            HookTopicEvents(flow);

            LogInfo("0010009");
            await flow.RunAsync(ct);
        }
        else {
            LogWarn("0010004", topic.GetType().Name);
        }
    }

    #endregion

    #region Topic Definition

    private TopicFlowActivity ToMarketingT1Topic(string id = "ToMarketingT1") {
        LogInfo("MT1_0001"); // ToMarketingT1Topic invoked

        return new TriggerTopicActivity(
            id,
            InsuranceTopicIds.MarketingT1,
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }

    private TopicFlowActivity ToMarketingT2Topic(string id = "ToMarketingT2") {
        LogInfo("MT2_0001"); // ToMarketingT2Topic invoked

        return new TriggerTopicActivity(
            id,
            InsuranceTopicIds.MarketingT2,
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }

    private TopicFlowActivity AskCaliforniaResidency(string id, TopicWorkflowContext context) {
        LogInfo("ACR_0001"); // Constructing card activity

        var cardActivity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            id,
            context,
            cardFactory: card => {
                LogInfo("ACR_0002"); // Card factory invoked

                var result = card.Create(
                    isResident: true,
                    zip_code: context.GetValue<string>("zip_code"),
                    ccpa_acknowledgment: context.GetValue<string?>("ccpa_acknoledgement")
                );

                if (result is AdaptiveCardModel model) {
                    LogInfo("ACR_0003"); // Card created successfully
                }
                else {
                    LogWarn("ACR_0004"); // Unexpected type returned
                }

                return result;
            });

        LogInfo("ACR_0005"); // Returning AdaptiveCardActivity
        return cardActivity;
    }

    private List<TopicFlowActivity>? ComplianceFlowActivities() {

        return new List<TopicFlowActivity> {

        // -------------------------------------------------------
        // TCPA = YES
        // -------------------------------------------------------
        FlowConditionHelpers.IfCase("TCPA_YES_BRANCH", ctx =>
            CoreContextExtensions.IsYes(ctx, "tcpa_consent"),

            ConditionalActivity<TopicFlowActivity>.If(
                "HAS_CA_INFO_YES_TCPA",
                c => CoreContextExtensions.IsYes(c, "is_california_resident"),

                // California Resident
                (id, c) => new CompositeActivity("ASK_CCPA_YES_CA", new List<TopicFlowActivity> {

                    AskCaliforniaResidency("CA_CARD_YES_CA", c),

                    ConditionalActivity<TopicFlowActivity>.If(
                        "HAS_CCPA_ACK",
                        cc => CoreContextExtensions.IsYes(cc, "ccpa_acknowledgment"),

                        (id2, cc) => ToMarketingT1Topic("AFTER_CCPA_YES"),
                        (id2, cc) => ToMarketingT2Topic("AFTER_CCPA_NO")
                    )
                }),

                // Not California
                (id, c) => ToMarketingT1Topic("NON_CA_TCPA_YES")
            )
        ),

        // -------------------------------------------------------
        // TCPA = NO
        // -------------------------------------------------------
        FlowConditionHelpers.IfCase("TCPA_NO", ctx =>
            CoreContextExtensions.IsNo(ctx, "tcpa_consent"),
            new TriggerTopicActivity(
                "TO_MARKETING_T3_AFTER_TCPA_NO",
                InsuranceTopicIds.MarketingT3,
                _logger,
                waitForCompletion: false,
                conversationContext: _context
            )
        )
    };
    }

    #endregion

    #region Abstract Event Handler Implementations

    /// <summary>
    /// Handles conversation start request from UI.
    /// </summary>
    protected override async Task OnConversationStartRequestedAsync(CancellationToken ct) {
        _logger.LogInformation("[InsuranceAgentServiceV2] Conversation start requested via event");
        await StartConversationAsync(ct);
    }

    /// <summary>
    /// Handles user message received from UI.
    /// </summary>
    protected override async Task OnUserMessageReceivedAsync(string message, CancellationToken ct) {
        _logger.LogInformation("[InsuranceAgentServiceV2] User message received via event: {Message}", message);
        await ProcessUserMessageAsync(message, ct);
    }

    /// <summary>
    /// Handles adaptive card submission from UI.
    /// </summary>
    protected override async Task OnCardSubmittedAsync(Dictionary<string, object> data, CancellationToken ct) {
        _logger.LogInformation("[InsuranceAgentServiceV2] Card submitted via event with {Count} fields", data.Count);
        await HandleCardSubmitAsync(data, ct);
    }

    /// <summary>
    /// Handles conversation reset request from UI.
    /// </summary>
    protected override async Task OnConversationResetRequestedAsync(CancellationToken ct) {
        _logger.LogInformation("[InsuranceAgentServiceV2] Conversation reset requested via event");
        await ResetConversationAsync(ct);
    }

    #endregion

    #region ChatWindow Event Subscription

    /// <summary>
    /// Wires up event subscriptions between UI and agent service.
    /// MUST be called after both ChatWindow and AgentService are created.
    /// </summary>
    public void SubscribeToChatWindowEvents(ConversaCore.UI.Components.CustomChatWindowV3 chatWindow)
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
        
        _logger.LogInformation("[InsuranceAgentServiceV2] ✅ Event subscriptions complete");
    }

    #endregion

    #region Domain-Specific Overrides

    // ============================================================
    // FIX: Background Semantic Activity Flow Continuation
    // ============================================================
    // PROBLEM:
    // When a SemanticQueryActivity runs with RunInBackground=true, it completes asynchronously
    // and triggers HandleAsyncActivityCompleted with a follow-up activity. The base class inserts
    // the activity via InsertNext(), but the flow remains stuck in WaitingForInput state and 
    // NEVER executes the inserted activity.
    //
    // ROOT CAUSE:
    // InsertNext() only adds the activity to the queue - it does NOT advance the flow execution.
    // The flow waits indefinitely for user input, so the inserted follow-up activity never runs.
    //
    // SOLUTION:
    // Subscribe to the PUBLIC AsyncActivityCompleted event (raised by base class). When triggered,
    // manually execute ONLY the inserted follow-up activity by calling its RunAsync() directly.
    // This avoids triggering StepAsync which would cascade through all subsequent activities.
    //
    // CRITICAL: We do NOT call flow.StepAsync() because:
    // 1. It would pass null as input to subsequent card activities
    // 2. It would cause the flow to advance through multiple activities at once
    // 3. Cards would appear one after another without waiting for user interaction
    //
    // Instead, we:
    // 1. Execute only the inserted follow-up activity (typically EventTriggerActivity)
    // 2. Let the activity complete and fire its events
    // 3. The flow remains in WaitingForInput state for the next card activity
    // ============================================================
    
    /// <summary>
    /// Hook into the base class's AsyncActivityCompleted event to execute follow-up activities.
    /// This must be called during initialization (e.g., in the constructor or a setup method).
    /// </summary>
    private void SetupAsyncActivityHandling()
    {
        // Subscribe to the PUBLIC event raised by base class after it inserts the follow-up activity
        this.AsyncActivityCompleted += OnAsyncActivityCompletedForExecution;
    }

    private void OnAsyncActivityCompletedForExecution(object? sender, AsyncQueryCompletedEventArgs e)
    {
        // If no follow-up activity or no active flow, nothing to do
        if (e.Activity == null || _activeTopic is not TopicFlow flow)
        {
            return;
        }

        var followup = e.Activity;

        // 🔍 DEBUG: Log current flow state
        _logger.LogWarning(
            "[DEBUG-ASYNC-DOMAIN] 🎯 InsuranceAgentServiceV2 AsyncActivityCompleted handler:\n" +
            "  Follow-up Activity: {ActivityId} ({ActivityType})\n" +
            "  Flow State: {FlowState}\n" +
            "  Current Activity: {CurrentActivity}",
            followup.Id,
            followup.GetType().Name,
            flow.State,
            flow.GetCurrentActivity()?.Id ?? "<none>"
        );

        // ✅ FIX: Execute ONLY the inserted follow-up activity without advancing the entire flow
        // Do NOT call flow.StepAsync() as it would cascade through all activities
        if (flow.State == TopicFlow.FlowState.WaitingForInput)
        {
            _logger.LogWarning(
                "[DEBUG-ASYNC-DOMAIN] 🚀 Flow is WaitingForInput - executing follow-up activity directly"
            );

            try
            {
                // Execute only the inserted activity without triggering full flow advancement
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogWarning(
                            "[DEBUG-ASYNC-DOMAIN] ⏳ Waiting 100ms before execution..."
                        );

                        await Task.Delay(100); // Small delay to ensure InsertNext completes

                        _logger.LogWarning(
                            "[DEBUG-ASYNC-DOMAIN] ▶️ Calling followup.RunAsync() for activity '{ActivityId}' ({ActivityType})",
                            followup.Id,
                            followup.GetType().Name
                        );

                        // Execute the follow-up activity directly without calling StepAsync
                        var result = await followup.RunAsync(flow.Context, null, CancellationToken.None);

                        _logger.LogWarning(
                            "[DEBUG-ASYNC-DOMAIN] ✅ Follow-up activity '{ActivityId}' executed successfully\n" +
                            "  Is Waiting: {IsWaiting}\n" +
                            "  Is End: {IsEnd}\n" +
                            "  Result Message: {ResultMessage}\n" +
                            "  Flow State After: {FlowState}\n" +
                            "  Current Activity After: {CurrentActivity}",
                            followup.Id,
                            result.IsWaiting,
                            result.IsEnd,
                            result.Message ?? "<none>",
                            flow.State,
                            flow.GetCurrentActivity()?.Id ?? "<none>"
                        );

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[DEBUG-ASYNC-DOMAIN] ❌ Failed to execute follow-up activity '{ActivityId}'",
                            followup.Id
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEBUG-ASYNC-DOMAIN] ❌ Failed to trigger follow-up activity execution");
            }
        }
        else
        {
            _logger.LogWarning(
                "[DEBUG-ASYNC-DOMAIN] ⚠️ Flow state is NOT WaitingForInput (State={FlowState}), skipping follow-up execution",
                flow.State
            );
        }
    }

    #endregion
}
