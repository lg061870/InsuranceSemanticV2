using ConversaCore.StateMachine;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Topics;
using InsuranceAgent.Models;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics;
using InsuranceAgent.Topics.CaliforniaResidentTopic;
using System.ComponentModel.DataAnnotations;

public class InsuranceAgentService {
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;
    private readonly TopicWorkflowContext _wfContext;
    private readonly ILogger<InsuranceAgentService> _logger;

    private ITopic? _activeTopic;
    private readonly Stack<TopicFlow> _pausedTopics = new Stack<TopicFlow>(); // Track paused topics

    // NEW: Event-driven sub-topic completion tracking
    private readonly Dictionary<string, PendingSubTopic> _pendingSubTopics = new Dictionary<string, PendingSubTopic>();

    private class PendingSubTopic {
        public TopicFlow CallingTopic { get; set; } = null!;
        public TopicFlow SubTopic { get; set; } = null!;
        public string CallingTopicName { get; set; } = string.Empty;
        public string SubTopicName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }

    // Outbound events → HybridChatService
    public event EventHandler<ActivityMessageEventArgs>? ActivityMessageReady;
    public event EventHandler<ActivityAdaptiveCardEventArgs>? ActivityAdaptiveCardReady;
    public event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;
    public event EventHandler<TopicLifecycleEventArgs>? TopicLifecycleChanged;
    public event EventHandler<MatchingTopicNotFoundEventArgs>? MatchingTopicNotFound;
    public event EventHandler<TopicInsertedEventArgs>? TopicInserted;
    public event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;
    public event EventHandler<ConversationResetEventArgs>? ConversationReset;

    public InsuranceAgentService(
        TopicRegistry topicRegistry,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<InsuranceAgentService> logger) {
        _topicRegistry = topicRegistry;
        _context = context;
        _wfContext = wfContext;
        _logger = logger;
    }

    public async Task ProcessUserMessageAsync(string userMessage, CancellationToken ct = default) {
        var (topic, confidence) = await _topicRegistry.FindBestTopicAsync(userMessage, _context, ct);

        if (topic == null) {
            _logger.LogWarning("No topic could handle '{Message}'", userMessage);
            MatchingTopicNotFound?.Invoke(this, new MatchingTopicNotFoundEventArgs(userMessage));
            return;
        }

        if (topic is TopicFlow flow) {
            // Unhook events from previous topic before switching
            if (_activeTopic is TopicFlow currentActiveTopic) {
                UnhookTopicEvents(currentActiveTopic);
            }

            _activeTopic = flow;
            HookTopicEvents(flow);

            _logger.LogInformation("Activated topic {TopicName} (confidence {Confidence:P2})",
                flow.Name, confidence);

            await flow.RunAsync(ct);
        }
        else {
            _logger.LogWarning("Resolved topic is not a TopicFlow: {Type}", topic.GetType().Name);
        }
    }

    public async Task HandleCardSubmitAsync(Dictionary<string, object> data, CancellationToken ct) {
        if (_activeTopic is TopicFlow flow) {
            var current = flow.GetCurrentActivity();

            if (current is IAdaptiveCardActivity cardAct) {
                _logger.LogInformation("[InsuranceAgentService] Delivering card input to {ActivityId}", current.Id);
                cardAct.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(data));

                // Instead of ResumeAsync, just advance the queue
                await flow.StepAsync(null, ct);
            }
            else {
                _logger.LogWarning("[InsuranceAgentService] Current activity {ActivityId} is not adaptive-card-capable", current?.Id);
                RaiseMessage("⚠️ This step cannot accept card input.");
            }
        }
        else {
            RaiseMessage("⚠️ Unable to resume workflow (no active topic).");
        }
    }

    private void HookTopicEvents(TopicFlow flow) {
        flow.TopicLifecycleChanged += (s, e) => TopicLifecycleChanged?.Invoke(this, e);
        flow.TopicLifecycleChanged += OnTopicLifecycleChanged; // For event-driven completion tracking
        flow.ActivityCreated += OnActivityCreated;

        flow.ActivityCompleted += (s, e) => {
            _logger.LogInformation("[InsuranceAgentService] ActivityCompleted -> {ActivityId}", e.ActivityId);
            ActivityCompleted?.Invoke(this, e);
        };

        flow.TopicInserted += (s, e) => TopicInserted?.Invoke(this, e);

        foreach (var act in flow.GetAllActivities()) {
            act.ActivityLifecycleChanged += (s, e) => {
                _logger.LogInformation("[InsuranceAgentService] ActivityLifecycleChanged: {ActivityId} -> {State} | Data={Data}",
                    e.ActivityId, e.State, e.Data);
                ActivityLifecycleChanged?.Invoke(this, e);
            };

            // Handle adaptive cards
            if (act is IAdaptiveCardActivity cardAct) {
                cardAct.CardJsonSent += (s, e) => {
                    _logger.LogInformation("[InsuranceAgentService] CardJsonSent {CardId}", e.CardId);
                    ActivityAdaptiveCardReady?.Invoke(this,
                        new ActivityAdaptiveCardEventArgs(e.CardJson ?? "{}", e.CardId, e.RenderMode));
                };

                cardAct.ValidationFailed += (s, e) => {
                    _logger.LogWarning("[InsuranceAgentService] ValidationFailed: {Error}", e.Exception?.Message);
                };

                cardAct.CardJsonEmitted += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardJsonEmitted (internal)");
                cardAct.CardJsonSending += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardJsonSending (internal)");
                cardAct.CardJsonRendered += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardJsonRendered (client ack)");
                cardAct.CardDataReceived += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardDataReceived (internal)");
                cardAct.ModelBound += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] ModelBound (internal)");
            }

            // Handle topic triggers
            if (act is TriggerTopicActivity trigger) {
                trigger.TopicTriggered += OnTopicTriggered;
            }

            // Handle topic triggers from ConditionalActivity containers
            // (child TriggerTopicActivity is created dynamically, so we subscribe to the container's forwarded event)
            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                conditional.TopicTriggered += OnTopicTriggered;
            }
        }
    }

    private void UnhookTopicEvents(TopicFlow flow) {
        // Unhook topic-level events
        flow.TopicLifecycleChanged -= (s, e) => TopicLifecycleChanged?.Invoke(this, e);
        flow.TopicLifecycleChanged -= OnTopicLifecycleChanged;
        flow.ActivityCreated -= OnActivityCreated;
        flow.ActivityCompleted -= (s, e) => {
            _logger.LogInformation("[InsuranceAgentService] ActivityCompleted -> {ActivityId}", e.ActivityId);
            ActivityCompleted?.Invoke(this, e);
        };
        flow.TopicInserted -= (s, e) => TopicInserted?.Invoke(this, e);

        // Unhook activity-level events
        foreach (var act in flow.GetAllActivities()) {
            if (act is TriggerTopicActivity trigger) {
                trigger.TopicTriggered -= OnTopicTriggered;
            }

            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                conditional.TopicTriggered -= OnTopicTriggered;
            }
        }
    }

    private void OnActivityCreated(object? sender, ActivityCreatedEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] ActivityCreated -> {PayloadType}: {Content}",
            e.Content?.GetType().Name, e.Content);

        switch (e.Content) {
            case string msg:
                RaiseMessage(msg);
                break;
            default:
                _logger.LogDebug("Unhandled activity payload type {Type}", e.Content?.GetType().Name);
                break;
        }
    }

    private void RaiseMessage(string content) {
        ActivityMessageReady?.Invoke(this,
            new ActivityMessageEventArgs(new ChatMessage {
                Content = content,
                IsFromUser = false,
                Timestamp = DateTime.Now
            }));
    }

    /// <summary>
    /// Adds domain-specific activities to the ConversationStartTopic.
    /// This ensures proper separation of concerns between system and domain activities.
    /// </summary>
    /// <param name="flow">The ConversationStartTopic flow instance</param>
    private void AddDomainActivitiesToStartTopic(TopicFlow flow) {
        _logger.LogInformation("[InsuranceAgentService] Adding domain-specific activities to ConversationStartTopic");

        // Add compliance trigger
        flow.Add(new TriggerTopicActivity("CollectCompliance", "ComplianceTopic", _logger, waitForCompletion: true));
        
        // Add a simple activity to process the compliance data before continuing
        flow.Add(new SimpleActivity("ProcessComplianceData", (ctx, input) => {
            _logger.LogInformation("[InsuranceAgentService] Processing compliance data before decision tree");
            // No additional processing needed, just act as a buffer between topic completion and decision tree
            return Task.FromResult<object?>(null);
        }));

        // Add the complex compliance flowchart
        flow.Add(
            ConditionalActivity<TopicFlowActivity>.Switch(
                "TCPAConsentSwitch",
                ctx => {
                    // Convert bool? to string for matching in the switch
                    var tcpaConsent = ctx.GetValue<bool?>("tcpa_consent");
                    return tcpaConsent == true ? "YES" : (tcpaConsent == false ? "NO" : "UNKNOWN");
                },
                new Dictionary<string, Func<string, TopicWorkflowContext, TopicFlowActivity>> {
                    // TCPA YES branch
                    ["YES"] = (id, ctx) => ConditionalActivity<TopicFlowActivity>.Switch(
                        "CCPAInitialSwitch",
                        ctx => {
                            // Convert bool? to string for matching in the switch
                            var ccpaAcknowledgment = ctx.GetValue<bool?>("ccpa_acknowledgment");
                            return ccpaAcknowledgment == true ? "YES" : (ccpaAcknowledgment == false ? "NO" : "UNKNOWN");
                        },
                        new Dictionary<string, Func<string, TopicWorkflowContext, TopicFlowActivity>> {
                            // TCPA YES + CCPA YES
                            ["YES"] = (id2, ctx2) => ConditionalActivity<TopicFlowActivity>.If(
                                "RESIDENCY_CHECK_TCPA_YES_CCPA_YES",
                                ctx2 => ctx2.GetValue<bool?>("is_california_resident").HasValue,
                                // Has CA residency information already - process it
                                (id3, ctx3) => ConditionalActivity<TopicFlowActivity>.If(
                                    "CA_VERIFY_YES",
                                    ctx3 => ctx3.GetValue<bool?>("is_california_resident") == true,
                                    // F_CA: Full marketing + CA protections - Trigger MarketingTypeOneTopic
                                    (id4, ctx4) => new TriggerTopicActivity(
                                        id4,
                                        "MarketingTypeOneTopic",
                                        _logger,
                                        waitForCompletion: false,
                                        conversationContext: _context
                                    ),
                                    // F_NON_CA_CORRECTED: Full marketing (CCPA corrected) - Trigger MarketingTypeOneTopic
                                    (id4, ctx4) => new TriggerTopicActivity(
                                        id4,
                                        "MarketingTypeOneTopic",
                                        _logger,
                                        waitForCompletion: false,
                                        conversationContext: _context
                                    )
                                ),
                                // Need to collect California residency info first
                                (id3, ctx3) => {
                                    var activity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
                                        "CA_VERIFY_TCPA_YES_CCPA_YES", 
                                        _wfContext,
                                        cardFactory: card => {
                                            // Setup card with appropriate messaging for this path
                                            var cardObj = card.Create();
                                            return cardObj;
                                        },
                                        onTransition: (from, to, data) => {
                                            // Only process after successful validation when activity is completed
                                            if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                                                // Store the residency status and zip validation result
                                                bool isCaliforniaResident = model.IsCaliforniaResident ?? false;
                                                bool hasValidZip = model.HasValidCaliforniaZip();
                                                _wfContext.SetValue("is_california_resident", isCaliforniaResident);
                                                _wfContext.SetValue("has_valid_ca_zip", hasValidZip);
                                                
                                                if (isCaliforniaResident && hasValidZip) {
                                                    // F_CA: Full marketing with CA protections
                                                    _wfContext.SetValue("marketing_path", "full_with_ca_protection");
                                                    _wfContext.SetValue("next_topic", "MarketingTypeOneTopic");
                                                } else {
                                                    // F_NON_CA: Full marketing standard path
                                                    _wfContext.SetValue("marketing_path", "full_marketing_standard");
                                                    _wfContext.SetValue("next_topic", "MarketingTypeOneTopic");
                                                }
                                            }
                                        }
                                    );
                                    
                                    return activity;
                                }
                            ),
                            // TCPA YES + CCPA NO
                            ["NO"] = (id2, ctx2) => new TriggerTopicActivity(
                                id2,
                                "MarketingTypeOneTopic",
                                _logger,
                                waitForCompletion: false,
                                conversationContext: _context
                            ),
                            // TCPA YES + CCPA UNKNOWN
                            ["UNKNOWN"] = (id2, ctx2) => ConditionalActivity<TopicFlowActivity>.If(
                                "RESIDENCY_CHECK_TCPA_YES_CCPA_UNKNOWN",
                                ctx2 => ctx2.GetValue<bool?>("is_california_resident").HasValue,
                                // Has CA residency information already - process it
                                (id3, ctx3) => ConditionalActivity<TopicFlowActivity>.If(
                                    "CA_VERIFY_UNKNOWN",
                                    ctx3 => ctx3.GetValue<bool?>("is_california_resident") == true,
                                    // H_CA: Marketing OK, mandatory CA protection
                                    (id4, ctx4) => new SimpleActivity(id4, (c, d) => {
                                        // H_CA
                                        c.SetValue("marketing_path", "marketing_with_ca_protection");
                                        c.SetValue("next_topic", "LeadQualificationTopic");
                                        return Task.FromResult<object?>(null);
                                    }),
                                    // H_NON_CA: Marketing OK, optional disclosures
                                    (id4, ctx4) => new SimpleActivity(id4, (c, d) => {
                                        // H_NON_CA
                                        c.SetValue("marketing_path", "marketing_optional_disclosure");
                                        c.SetValue("next_topic", "LeadQualificationTopic");
                                        return Task.FromResult<object?>(null);
                                    })
                                ),
                                // Need to collect California residency info first
                                (id3, ctx3) => {
                                    var activity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
                                        "CA_VERIFY_TCPA_YES_CCPA_UNKNOWN", 
                                        _wfContext,
                                        cardFactory: card => {
                                            // Setup card with appropriate messaging for this path
                                            var cardObj = card.Create();
                                            return cardObj;
                                        },
                                        onTransition: (from, to, data) => {
                                            // Only process after successful validation when activity is completed
                                            if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                                                // Store the residency status and zip validation result
                                                bool isCaliforniaResident = model.IsCaliforniaResident ?? false;
                                                bool hasValidZip = model.HasValidCaliforniaZip();
                                                _wfContext.SetValue("is_california_resident", isCaliforniaResident);
                                                _wfContext.SetValue("has_valid_ca_zip", hasValidZip);
                                                
                                                if (isCaliforniaResident && hasValidZip) {
                                                    // H_CA: Marketing OK, mandatory CA protection
                                                    _wfContext.SetValue("marketing_path", "marketing_with_ca_protection");
                                                    _wfContext.SetValue("next_topic", "MarketingTypeOneTopic");
                                                } else {
                                                    // H_NON_CA: Marketing OK, optional disclosures
                                                    _wfContext.SetValue("marketing_path", "marketing_optional_disclosure");
                                                    _wfContext.SetValue("next_topic", "MarketingTypeOneTopic");
                                                }
                                            }
                                        }
                                    );
                                    
                                    return activity;
                                }
                            )
                        },
                        defaultBranch: "UNKNOWN"
                    ),
                    // TCPA NO branch
                    ["NO"] = (id, ctx) => ConditionalActivity<TopicFlowActivity>.Switch(
                        "CCPA_NO_TCPA_Switch",
                        ctx => {
                            // Convert bool? to string for matching in the switch
                            var ccpaAcknowledgment = ctx.GetValue<bool?>("ccpa_acknowledgment");
                            return ccpaAcknowledgment == true ? "YES" : (ccpaAcknowledgment == false ? "NO" : "UNKNOWN");
                        },
                        new Dictionary<string, Func<string, TopicWorkflowContext, TopicFlowActivity>> {
                            // TCPA NO + CCPA YES
                            ["YES"] = (id2, ctx2) => ConditionalActivity<TopicFlowActivity>.If(
                                "RESIDENCY_CHECK_TCPA_NO_CCPA_YES",
                                ctx2 => ctx2.GetValue<bool?>("is_california_resident").HasValue,
                                // Has CA residency information already - process it
                                (id3, ctx3) => ConditionalActivity<TopicFlowActivity>.If(
                                    "CA_VERIFY_TCPA_NO_CCPA_YES",
                                    ctx3 => ctx3.GetValue<bool?>("is_california_resident") == true,
                                    // I_CA: No marketing, CA disclosures required
                                    (id4, ctx4) => new SimpleActivity(id4, (c, d) => {
                                        // I_CA
                                        c.SetValue("marketing_path", "no_marketing_ca_disclosure");
                                        c.SetValue("next_topic", "InformationalContentTopic");
                                        return Task.FromResult<object?>(null);
                                    }),
                                    // I_NON_CA: No marketing, optional disclosures
                                    (id4, ctx4) => new SimpleActivity(id4, (c, d) => {
                                        // I_NON_CA
                                        c.SetValue("marketing_path", "no_marketing_optional");
                                        c.SetValue("next_topic", "InformationalContentTopic");
                                        return Task.FromResult<object?>(null);
                                    })
                                ),
                                // Need to collect California residency info first
                                (id3, ctx3) => {
                                    var activity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
                                        "CA_VERIFY_TCPA_NO_CCPA_YES", 
                                        _wfContext,
                                        cardFactory: card => {
                                            // Setup card with appropriate messaging for this path
                                            var cardObj = card.Create();
                                            return cardObj;
                                        },
                                        onTransition: (from, to, data) => {
                                            // Only process after successful validation when activity is completed
                                            if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                                                // Store the residency status and zip validation result
                                                bool isCaliforniaResident = model.IsCaliforniaResident ?? false;
                                                bool hasValidZip = model.HasValidCaliforniaZip();
                                                _wfContext.SetValue("is_california_resident", isCaliforniaResident);
                                                _wfContext.SetValue("has_valid_ca_zip", hasValidZip);
                                                
                                                if (isCaliforniaResident && hasValidZip) {
                                                    // I_CA: No marketing, CA disclosures required
                                                    _wfContext.SetValue("marketing_path", "no_marketing_ca_disclosure");
                                                    _wfContext.SetValue("next_topic", "InformationalContentTopic");
                                                } else {
                                                    // I_NON_CA: No marketing, optional disclosures
                                                    _wfContext.SetValue("marketing_path", "no_marketing_optional");
                                                    _wfContext.SetValue("next_topic", "InformationalContentTopic");
                                                }
                                            }
                                        }
                                    );
                                    
                                    return activity;
                                }
                            ),
                            // TCPA NO + CCPA NO
                            ["NO"] = (id2, ctx2) => new SimpleActivity(id2, (c, d) => {
                                // J_NON_CA
                                c.SetValue("marketing_path", "blocked_minimal");
                                c.SetValue("next_topic", "BasicNavigationTopic");
                                return Task.FromResult<object?>(null);
                            }),
                            // TCPA NO + CCPA UNKNOWN
                            ["UNKNOWN"] = (id2, ctx2) => ConditionalActivity<TopicFlowActivity>.If(
                                "RESIDENCY_CHECK_TCPA_NO_CCPA_UNKNOWN",
                                ctx2 => ctx2.GetValue<bool?>("is_california_resident").HasValue,
                                // Has CA residency information already - process it
                                (id3, ctx3) => ConditionalActivity<TopicFlowActivity>.If(
                                    "CA_VERIFY_TCPA_NO_CCPA_UNKNOWN",
                                    ctx3 => ctx3.GetValue<bool?>("is_california_resident") == true,
                                    // K_CA: No marketing, mandatory CA protection
                                    (id4, ctx4) => new SimpleActivity(id4, (c, d) => {
                                        // K_CA
                                        c.SetValue("marketing_path", "no_marketing_mandatory_ca");
                                        c.SetValue("next_topic", "InformationalContentTopic");
                                        return Task.FromResult<object?>(null);
                                    }),
                                    // K_NON_CA: No marketing, minimal protection
                                    (id4, ctx4) => new SimpleActivity(id4, (c, d) => {
                                        // K_NON_CA
                                        c.SetValue("marketing_path", "no_marketing_minimal");
                                        c.SetValue("next_topic", "InformationalContentTopic");
                                        return Task.FromResult<object?>(null);
                                    })
                                ),
                                // Need to collect California residency info first
                                (id3, ctx3) => {
                                    var activity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
                                        "CA_VERIFY_TCPA_NO_CCPA_UNKNOWN", 
                                        _wfContext,
                                        cardFactory: card => {
                                            // Setup card with appropriate messaging for this path
                                            var cardObj = card.Create();
                                            return cardObj;
                                        },
                                        onTransition: (from, to, data) => {
                                            // Only process after successful validation when activity is completed
                                            if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                                                // Store the residency status and zip validation result
                                                bool isCaliforniaResident = model.IsCaliforniaResident ?? false;
                                                bool hasValidZip = model.HasValidCaliforniaZip();
                                                _wfContext.SetValue("is_california_resident", isCaliforniaResident);
                                                _wfContext.SetValue("has_valid_ca_zip", hasValidZip);
                                                
                                                if (isCaliforniaResident && hasValidZip) {
                                                    // K_CA: No marketing, mandatory CA protection
                                                    _wfContext.SetValue("marketing_path", "no_marketing_mandatory_ca");
                                                    _wfContext.SetValue("next_topic", "InformationalContentTopic");
                                                } else {
                                                    // K_NON_CA: No marketing, minimal protection
                                                    _wfContext.SetValue("marketing_path", "no_marketing_minimal");
                                                    _wfContext.SetValue("next_topic", "InformationalContentTopic");
                                                }
                                            }
                                        }
                                    );
                                    
                                    return activity;
                                }
                            )
                        },
                        defaultBranch: "UNKNOWN"
                    ),
                    // TCPA UNKNOWN branch
                    ["UNKNOWN"] = (id, ctx) => ConditionalActivity<TopicFlowActivity>.Switch(
                        "CCPA_UNKNOWN_TCPA_Switch",
                        ctx => {
                            // Convert bool? to string for matching in the switch
                            var ccpaAcknowledgment = ctx.GetValue<bool?>("ccpa_acknowledgment");
                            return ccpaAcknowledgment == true ? "YES" : (ccpaAcknowledgment == false ? "NO" : "UNKNOWN");
                        },
                        new Dictionary<string, Func<string, TopicWorkflowContext, TopicFlowActivity>> {
                            // TCPA UNKNOWN + CCPA YES - Show California residency verification card
                            ["YES"] = (id2, ctx2) => {
                                var activity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
                                    "CA_VERIFY_TCPA_UNKNOWN_CCPA_YES", 
                                    _wfContext,
                                    cardFactory: card => {
                                        // Setup card with appropriate messaging for this path
                                        var cardObj = card.Create();
                                        // Note: Cannot modify card title/description directly as it's in the JSON structure
                                        return cardObj;
                                    },
                                    onTransition: (from, to, data) => {
                                        // Only process after successful validation when activity is completed
                                        if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                                            // Store the residency status and zip validation result
                                            // This happens AFTER validation has succeeded
                                            bool isCaliforniaResident = model.IsCaliforniaResident ?? false;
                                            bool hasValidZip = model.HasValidCaliforniaZip();
                                            _wfContext.SetValue("is_california_resident", isCaliforniaResident);
                                            _wfContext.SetValue("has_valid_ca_zip", hasValidZip);
                                            
                                            if (isCaliforniaResident && hasValidZip) {
                                                // L_CA: No marketing - TCPA risk + CA requirements
                                                _wfContext.SetValue("marketing_path", "no_marketing_tcpa_risk_ca");
                                                _wfContext.SetValue("next_topic", "InformationalContentTopic");
                                            } else {
                                                // L_NON_CA: No marketing - TCPA risk only
                                                _wfContext.SetValue("marketing_path", "no_marketing_tcpa_risk");
                                                _wfContext.SetValue("next_topic", "InformationalContentTopic");
                                            }
                                        }
                                    }
                                );
                                
                                // Add validation failure handler
                                activity.ValidationFailed += (s, e) => {
                                    _logger.LogWarning("[CA Resident] TCPA_UNKNOWN_CCPA_YES Validation failed: {Error}", 
                                        e.Exception?.Message ?? "No specific error");
                                    
                                    // Just log the validation error for debugging
                                    _logger.LogWarning("[CA Resident] Exception type: {Type}", 
                                        e.Exception?.GetType().Name ?? "Unknown");
                                };
                                
                                return activity;
                            },
                            // TCPA UNKNOWN + CCPA NO
                            ["NO"] = (id2, ctx2) => new SimpleActivity(id2, (c, d) => {
                                // M_NON_CA
                                c.SetValue("marketing_path", "maximum_restriction");
                                c.SetValue("next_topic", "BasicNavigationTopic");
                                return Task.FromResult<object?>(null);
                            }),
                            // TCPA UNKNOWN + CCPA UNKNOWN - Show California residency verification card
                            ["UNKNOWN"] = (id2, ctx2) => {
                                var activity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
                                    "CA_VERIFY_TCPA_UNKNOWN_CCPA_UNKNOWN", 
                                    _wfContext,
                                    cardFactory: card => {
                                        // Setup card with appropriate messaging for this path
                                        var cardObj = card.Create();
                                        // Note: Cannot modify card title/description directly as it's in the JSON structure
                                        return cardObj;
                                    },
                                    onTransition: (from, to, data) => {
                                        // Only process after successful validation when activity is completed
                                        if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                                            // Store the residency status and zip validation result
                                            // This happens AFTER validation has succeeded
                                            bool isCaliforniaResident = model.IsCaliforniaResident ?? false;
                                            bool hasValidZip = model.HasValidCaliforniaZip();
                                            _wfContext.SetValue("is_california_resident", isCaliforniaResident);
                                            _wfContext.SetValue("has_valid_ca_zip", hasValidZip);
                                            
                                            if (isCaliforniaResident && hasValidZip) {
                                                // N_CA: Conservative + mandatory CA protection
                                                _wfContext.SetValue("marketing_path", "conservative_ca_protection");
                                                _wfContext.SetValue("next_topic", "BasicNavigationTopic");
                                            } else {
                                                // N_NON_CA: Conservative approach
                                                _wfContext.SetValue("marketing_path", "conservative_approach");
                                                _wfContext.SetValue("next_topic", "BasicNavigationTopic");
                                            }
                                        }
                                    }
                                );
                                
                                return activity;
                            }
                        },
                        defaultBranch: "UNKNOWN"
                    )
                },
                defaultBranch: "UNKNOWN"
            )
        );

        _logger.LogInformation("[InsuranceAgentService] Domain-specific activities added to ConversationStartTopic");
    }

    public async Task StartConversationAsync(ChatSessionState sessionState, CancellationToken ct = default) {
        // Clear any previous execution state
        _pausedTopics.Clear();

        // >>> this method is called from HybridChatAgent where we already retrieved the topic, yet we do it here again???
        // NOTE: Only one retrieval is needed. Refactor to avoid duplicate lookups in future.
        var topic = _topicRegistry.GetTopic("ConversationStart");

        if (topic == null) {
            _logger.LogWarning("ConversationStartTopic not found in registry.");
            return;
        }

        if (topic is TopicFlow flow) {
            // Make sure we're not duplicating activities
            // Remove existing activities with the same IDs to be safe
            flow.RemoveActivity("CollectCompliance");
            flow.RemoveActivity("ProcessComplianceData");
            flow.RemoveActivity("TCPAConsentSwitch");
            
            // Now use the extracted method to add domain-specific activities
            AddDomainActivitiesToStartTopic(flow);

            // Unhook events from previous topic before switching
            if (_activeTopic is TopicFlow currentActiveTopic) {
                UnhookTopicEvents(currentActiveTopic);
            }

            _activeTopic = flow;
            HookTopicEvents(flow);

            _logger.LogInformation("ConversationStartTopic activated");
            await flow.RunAsync(ct);
        }
        else {
            _logger.LogWarning("ConversationStartTopic is not a TopicFlow: {Type}", topic.GetType().Name);
        }
    }

    public async Task ResetConversationAsync(CancellationToken ct = default) {
        _logger.LogInformation("[InsuranceAgentService] Resetting conversation");

        // Clear all state
        _pausedTopics.Clear();
        _pendingSubTopics.Clear();

        // Unhook current topic events
        if (_activeTopic is TopicFlow currentTopic) {
            UnhookTopicEvents(currentTopic);
        }
        _activeTopic = null;

        // Clear contexts
        _context.Reset();
        _wfContext.Clear();

        // --- FULL TOPIC STATE RESET ---
        // Reset ALL topics to ensure clean state
        var allTopics = _topicRegistry.GetAllTopics();
        foreach (var topic in allTopics) {
            if (topic is TopicFlow topicFlow) {
                _logger.LogInformation("[InsuranceAgentService] Resetting topic: {TopicName}", topic.Name);
                topicFlow.Reset();
            }
        }
        
        // --- SPECIAL HANDLING FOR CONVERSATION START TOPIC ---
        // This is critical for properly restarting the conversation
        var conversationStartTopic = _topicRegistry.GetTopic("ConversationStart") as TopicFlow;
        if (conversationStartTopic != null) {
            // Clear both context flags to be thorough
            conversationStartTopic.Context.SetValue("ConversationStartTopic.HasRun", false);
            _context.SetValue("ConversationStartTopic.HasRun", false);
            
            // Force clear any other related flags that might prevent restart
            _context.SetValue("Global_ConversationActive", false);
            _context.SetValue("Global_TopicHistory", new List<string>());
            _context.SetValue("Global_UserInteractionCount", 0);
            
            // Ensure state machine is properly reset
            // Force ConversationStartTopic to Idle state to fix issues with multiple resets
            // This uses reflection to access the protected _fsm field
            var stateMachine = conversationStartTopic.GetType().BaseType?.GetField("_fsm", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance)?.GetValue(conversationStartTopic);
            
            if (stateMachine is ITopicStateMachine<ConversaCore.TopicFlow.TopicFlow.FlowState> fsm) {
                // Use our new ForceState method to guarantee proper state
                fsm.ForceState(ConversaCore.TopicFlow.TopicFlow.FlowState.Idle, 
                    "Forced reset to Idle in ResetConversationAsync");
                
                // Also clear transition history for a clean slate
                fsm.ClearTransitionHistory();
                
                _logger.LogInformation("[InsuranceAgentService] ConversationStartTopic state machine forced to Idle state");
            }
            
            // CRITICAL: Don't add domain activities here, they will be added in StartConversationAsync
            // Removed: AddDomainActivitiesToStartTopic(conversationStartTopic);
            
            // CRITICAL FIX: Also force ComplianceTopic to Idle state and clear all its flags
            var complianceTopic = _topicRegistry.GetTopic("ComplianceTopic") as TopicFlow;
            if (complianceTopic != null) {
                // Force reset state machine
                var complianceStateMachine = complianceTopic.GetType().BaseType?.GetField("_fsm", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance)?.GetValue(complianceTopic);
                
                if (complianceStateMachine is ITopicStateMachine<ConversaCore.TopicFlow.TopicFlow.FlowState> complianceFsm) {
                    complianceFsm.ForceState(ConversaCore.TopicFlow.TopicFlow.FlowState.Idle, 
                        "Forced reset to Idle in ResetConversationAsync");
                    complianceFsm.ClearTransitionHistory();
                    _logger.LogInformation("[InsuranceAgentService] ComplianceTopic state machine forced to Idle state");
                }
                
                // Clear any activity flags
                _wfContext.SetValue("ShowComplianceCard_Sent", null);
                _wfContext.SetValue("ShowComplianceCard_Rendered", null);
                _wfContext.SetValue("ShowComplianceCard_Completed", null);
                _wfContext.SetValue("ComplianceTopic_Completed", null);
                _wfContext.SetValue("ComplianceTopic_HasRun", null);
                
                // Clear out any compliance data to force a fresh card
                // Set empty/default values instead of trying to remove keys
                _context.SetValue("compliance_data", new Dictionary<string, object>());
                _context.SetValue("tcpa_consent", false);
                _context.SetValue("ccpa_acknowledgment", false);
                
                _logger.LogInformation("[InsuranceAgentService] ComplianceTopic activity flags and compliance data cleared");
            }
            
            _logger.LogInformation("[InsuranceAgentService] ConversationStartTopic.HasRun flag cleared and conversation flags reset");
        }
        
        // Log completion of reset to help with debugging
        _logger.LogInformation("[InsuranceAgentService] Conversation reset completed - all topics and context cleared");

        // Fire reset event for UI
        ConversationReset?.Invoke(this, new ConversationResetEventArgs());

        // Add a small delay to ensure UI has time to process the reset
        await Task.Delay(100, ct);

        // Start fresh conversation
        await StartConversationAsync(new ChatSessionState(), ct);
    }

    /// <summary>
    /// Handles the completion of a sub-topic and resumes the calling topic.
    /// </summary>
    private async Task HandleSubTopicCompletion(TopicFlow completedSubTopic, TopicResult subTopicResult) {
        if (_pausedTopics.Count == 0) {
            _logger.LogWarning("[InsuranceAgentService] Sub-topic '{SubTopic}' completed but no paused topics to resume",
                completedSubTopic.Name);
            return;
        }

        var callingTopic = _pausedTopics.Pop();
        _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' completed, resuming '{CallingTopic}'",
            completedSubTopic.Name, callingTopic.Name);

        // Set completion data in context for the calling topic to access
        var completionData = new Dictionary<string, object>();
        if (subTopicResult.wfContext != null) {
            // Copy relevant data from the sub-topic's workflow context
            foreach (var key in subTopicResult.wfContext.GetKeys()) {
                var value = subTopicResult.wfContext.GetValue<object>(key);
                if (value != null) {
                    completionData[key] = value;
                }
            }
        }

        _wfContext.SetValue("SubTopicCompletionData", completionData);

        // Pop the topic call from conversation context
        var callInfo = _context.PopTopicCall(completionData);
        if (callInfo != null) {
            _wfContext.SetValue("ResumeData", callInfo.ResumeData);
            _logger.LogInformation("[InsuranceAgentService] Call info retrieved: {CallingTopic} -> {SubTopic}",
                callInfo.CallingTopicName, callInfo.SubTopicName);
        }

        // Resume the calling topic
        // Unhook events from current active topic before switching
        if (_activeTopic is TopicFlow currentActiveTopic) {
            UnhookTopicEvents(currentActiveTopic);
        }

        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);

        try {
            // Continue execution from where it left off
            await callingTopic.ResumeAsync("Sub-topic completed", CancellationToken.None);
        } catch (Exception ex) {
            _logger.LogError(ex, "[InsuranceAgentService] Error resuming topic '{TopicName}' after sub-topic completion",
                callingTopic.Name);
        }
    }

    private async void OnTopicTriggered(object? sender, TopicTriggeredEventArgs e) {
        // Extract WaitForCompletion from the TriggerTopicActivity sender
        if (sender is not TriggerTopicActivity trigger) {
            _logger.LogWarning("[InsuranceAgentService] Topic triggered by unsupported sender type: {SenderType}", sender?.GetType().Name);
            return;
        }

        var waitForCompletion = trigger.WaitForCompletion;
        _logger.LogInformation("[InsuranceAgentService] Topic triggered: {TopicName} (WaitForCompletion: {WaitForCompletion})",
            e.TopicName, waitForCompletion);

        var nextTopic = _topicRegistry.GetTopic(e.TopicName);
        if (nextTopic is TopicFlow nextFlow) {

            if (waitForCompletion) {
                // NEW: Sub-topic pattern - pause current topic
                if (_activeTopic is TopicFlow currentFlow) {
                    _logger.LogInformation("[InsuranceAgentService] Pausing topic '{CurrentTopic}' for sub-topic '{SubTopic}'",
                        currentFlow.Name, nextFlow.Name);
                    _pausedTopics.Push(currentFlow);
                }
            }

            // Unhook events from previous topic before switching
            if (_activeTopic is TopicFlow currentActiveTopic) {
                UnhookTopicEvents(currentActiveTopic);
            }

            _activeTopic = nextFlow;
            HookTopicEvents(nextFlow);

            if (waitForCompletion) {
                // Get the calling topic from paused topics stack
                var callingTopic = _pausedTopics.Count > 0 ? _pausedTopics.Peek() : null;
                if (callingTopic != null) {
                    // Register for event-driven completion tracking
                    _pendingSubTopics[e.TopicName] = new PendingSubTopic {
                        CallingTopic = callingTopic,
                        SubTopic = nextFlow,
                        CallingTopicName = callingTopic.Name,
                        SubTopicName = e.TopicName,
                        StartTime = DateTime.UtcNow
                    };

                    _logger.LogInformation("[InsuranceAgentService] Registered sub-topic '{SubTopic}' for completion tracking", e.TopicName);
                }
                else {
                    _logger.LogWarning("[InsuranceAgentService] WaitForCompletion=true but no calling topic found in paused stack");
                }
            }

            var result = await nextFlow.RunAsync();

            _logger.LogInformation("[InsuranceAgentService] Sub-topic completed. WaitForCompletion: {WaitForCompletion}, IsCompleted: {IsCompleted}",
                waitForCompletion, result.IsCompleted);

            // Only handle immediate completion for legacy topics or actually completed topics
            if (!waitForCompletion && result.IsCompleted) {
                _logger.LogInformation("[InsuranceAgentService] Calling HandleSubTopicCompletion for legacy topic {TopicName}", e.TopicName);
                await HandleSubTopicCompletion(nextFlow, result);
            }
            else if (waitForCompletion && result.IsCompleted) {
                _logger.LogInformation("[InsuranceAgentService] Sub-topic completed immediately, calling HandleSubTopicCompletion for {TopicName}", e.TopicName);
                // Remove from pending since it completed immediately
                _pendingSubTopics.Remove(e.TopicName);
                await HandleSubTopicCompletion(nextFlow, result);
            }
            else if (waitForCompletion) {
                _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' started, waiting for completion event", e.TopicName);
            }
            // For WaitForCompletion=true and IsCompleted=false, the TopicLifecycleChanged event will handle completion
        }
        else {
            _logger.LogWarning("[InsuranceAgentService] Triggered topic {TopicName} not found or not a TopicFlow.", e.TopicName);
        }
    }

    private async void OnTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] TopicLifecycleChanged: {TopicName} -> {State}", e.TopicName, e.State);

        // Handle sub-topic completion
        if (e.State == TopicLifecycleState.Completed && _pendingSubTopics.ContainsKey(e.TopicName)) {
            var pendingSubTopic = _pendingSubTopics[e.TopicName];
            _pendingSubTopics.Remove(e.TopicName);

            _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' completed via lifecycle event, resuming '{CallingTopic}'",
                e.TopicName, pendingSubTopic.CallingTopicName);

            // Create a synthetic TopicResult for the completed sub-topic
            var subTopicResult = TopicResult.CreateCompleted("Sub-topic completed", pendingSubTopic.SubTopic.Context);

            await HandleSubTopicCompletion(pendingSubTopic.SubTopic, subTopicResult);
        }
    }
}
