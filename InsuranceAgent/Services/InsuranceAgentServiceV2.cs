using ConversaCore.Cards;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using ConversaCore.Agentic;
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
            "MarketingT1Topic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }

    private TopicFlowActivity ToMarketingT2Topic(string id = "ToMarketingT2") {
        LogInfo("MT2_0001"); // ToMarketingT2Topic invoked

        return new TriggerTopicActivity(
            id,
            "MarketingT2Topic",
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
        IfCase("TCPA_YES_BRANCH", ctx =>
            IsYes(ctx, "tcpa_consent"),

            ConditionalActivity<TopicFlowActivity>.If(
                "HAS_CA_INFO_YES_TCPA",
                c => IsYes(c, "is_california_resident"),

                // California Resident
                (id, c) => new CompositeActivity("ASK_CCPA_YES_CA", new List<TopicFlowActivity> {

                    AskCaliforniaResidency("CA_CARD_YES_CA", c),

                    ConditionalActivity<TopicFlowActivity>.If(
                        "HAS_CCPA_ACK",
                        cc => IsYes(cc, "ccpa_acknowledgment"),

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
        IfCase("TCPA_NO", ctx =>
            IsNo(ctx, "tcpa_consent"),
            new TriggerTopicActivity(
                "TO_MARKETING_T3_AFTER_TCPA_NO",
                "MarketingT3Topic",
                _logger,
                waitForCompletion: false,
                conversationContext: _context
            )
        )
    };
    }

    #endregion

    //#region Abstract Event Handler Implementations

    ///// <summary>
    ///// Handles conversation start request from UI.
    ///// </summary>
    //protected override async Task OnConversationStartRequestedAsync(CancellationToken ct)
    //{
    //    _logger.LogInformation("[InsuranceAgentServiceV2] Conversation start requested via event");
    //    await StartConversationAsync(ct);
    //}

    ///// <summary>
    ///// Handles user message received from UI.
    ///// </summary>
    //protected override async Task OnUserMessageReceivedAsync(string message, CancellationToken ct)
    //{
    //    _logger.LogInformation("[InsuranceAgentServiceV2] User message received via event: {Message}", message);
    //    await ProcessUserMessageAsync(message, ct);
    //}

    ///// <summary>
    ///// Handles adaptive card submission from UI.
    ///// </summary>
    //protected override async Task OnCardSubmittedAsync(Dictionary<string, object> data, CancellationToken ct)
    //{
    //    _logger.LogInformation("[InsuranceAgentServiceV2] Card submitted via event with {Count} fields", data.Count);
    //    await HandleCardSubmitAsync(data, ct);
    //}

    ///// <summary>
    ///// Handles conversation reset request from UI.
    ///// </summary>
    //protected override async Task OnConversationResetRequestedAsync(CancellationToken ct)
    //{
    //    _logger.LogInformation("[InsuranceAgentServiceV2] Conversation reset requested via event");
    //    await ResetConversationAsync(ct);
    //}

    //#endregion

    #region ChatWindow Event Subscription

    /// <summary>
    /// Wires up event subscriptions between UI and agent service.
    /// MUST be called after both ChatWindow and AgentService are created.
    /// </summary>
    public void SubscribeToChatWindowEvents(InsuranceAgent.Pages.Components.CustomChatWindowV3 chatWindow)
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
}
