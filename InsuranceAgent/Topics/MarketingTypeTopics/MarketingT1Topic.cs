using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Builders;
using InsuranceAgent.Cards;
using InsuranceAgent.DomainTypes;
using InsuranceAgent.Models;
using InsuranceAgent.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using System.Threading.Tasks;

namespace InsuranceAgent.Topics;

/// <summary>
/// MarketingT1Topic handles the full-consent lead qualification sequence (TCPA + CCPA approved).
/// It collects lead details, life goals, and other profile data through adaptive cards.
/// Between cards, it emits *semantic* custom events that the UI can listen to (e.g. CustomerConsole).
/// </summary>
public class MarketingT1Topic : TopicFlow {
    private readonly ILogger<MarketingT1Topic> _logger;
    private readonly IConversationContext _conversationContext;
    private readonly Kernel _kernel;
    private readonly InsuranceRuleRepository _insuranceRuleRepository;

    public static readonly string[] IntentKeywords = new[]
    {
        "full marketing", "type 1", "t1", "complete path",
        "full qualification", "marketing path one", "lead qualification"
    };

    public MarketingT1Topic(
        TopicWorkflowContext context,
        ILogger<MarketingT1Topic> logger,
        IConversationContext conversationContext,
        Kernel kernel,
        InsuranceRuleRepository insuranceRuleRepository)
        : base(context, logger, name: "MarketingT1Topic") {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _conversationContext = conversationContext ?? throw new ArgumentNullException(nameof(conversationContext));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _insuranceRuleRepository = insuranceRuleRepository ?? throw new ArgumentNullException(nameof(insuranceRuleRepository));

        Context.SetValue("TopicName", "Marketing Path Type 1");
        Context.SetValue("marketing_path_type", "T1");
        Context.SetValue("MarketingT1Topic_create", DateTime.UtcNow.ToString("o"));

        _ = Task.Run(async () =>
        {
            try {
                await InitializeActivitiesAsync();
                _logger.LogInformation("[MarketingT1Topic] ✅ Async initialization completed successfully.");
            } catch (Exception ex) {
                _logger.LogError(ex, "[MarketingT1Topic] ❌ Failed during async initialization.");
            }
        });
    }

    public override async void Reset() {
        _logger.LogInformation("[MarketingT1Topic] Resetting topic and reinitializing activities");
        base.Reset();
        await InitializeActivitiesAsync();
    }

    private async Task InitializeActivitiesAsync() {
        ClearActivities();

        // --- Resolve active rule set based on life goals ---
        var activeRuleSets = await RuleSelector.SelectRulesAsync(Context, _insuranceRuleRepository);

        // === INITIALIZATION ===
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "customer_console_show",
            data: new { message = "Displaying customer console for lead qualification" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        // === LEAD DETAILS ===
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "lead_details_started",
            data: new { stage = "lead-details", message = "Collecting lead details" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        // === CONTACT INFO ===
        Add(new AdaptiveCardActivity<ContactInfoCard, ContactInfoModel>(
            "ContactInfo", Context,
            cardFactory: c => c.Create(
                fullName: Context.GetValue<string>("full_name"),
                phoneNumber: Context.GetValue<string>("phone_number"),
                emailAddress: Context.GetValue<string>("email_address"),
                dateOfBirth: Context.GetValue<string>("date_of_birth"),
                streetAddress: Context.GetValue<string>("street_address"),
                city: Context.GetValue<string>("city"),
                state: Context.GetValue<string>("state"),
                zipCode: Context.GetValue<string>("zip_code"),
                bestContactTime: Context.GetValue<string>("best_contact_time"),
                contactMethod: Context.GetValue<string>("contact_method"),
                consentContact: Context.GetValue<bool>("consent_contact")
            )

        ));

        Add(new AdaptiveCardActivity<LeadDetailsCard, LeadDetailsModel>(
            "LeadDetails", Context,
            cardFactory: c => c.Create(
                language: Context.GetValue<string>("language"),
                leadSource: Context.GetValue<string>("lead_source"),
                interestLevel: Context.GetValue<string>("interest_level"),
                leadIntent: Context.GetValue<string>("lead_intent")
            )
        ));

        // contact_info_submitted
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "contact_info_submitted",
            data: new { stage = "contact-info", progress = 10, message = "Contact info verified" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        // lead_details_submitted
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "lead_details_submitted",
            data: new { stage = "lead-details", progress = 20, message = "Lead details collected" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        // === LIFE GOALS ===
        Add(new AdaptiveCardActivity<LifeGoalsCard, LifeGoalsModel>(
            "LifeGoals", Context,
            cardFactory: c => c.Create(
                protectLovedOnes: Context.GetValue<bool?>("intent_protect_loved_ones"),
                payMortgage: Context.GetValue<bool?>("intent_pay_mortgage"),
                prepareFuture: Context.GetValue<bool?>("intent_prepare_future"),
                peaceOfMind: Context.GetValue<bool?>("intent_peace_of_mind"),
                coverExpenses: Context.GetValue<bool?>("intent_cover_expenses"),
                unsure: Context.GetValue<bool?>("intent_unsure")
            )
        ));

        // life_goals_submitted
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "life_goals_submitted",
            data: new { stage = "life-goals", progress = 40, message = "Life goals recorded" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        // === HEALTH INFO ===
        Add(new AdaptiveCardActivity<HealthInfoCard, HealthInfoModel>(
            "HealthInfo", Context,
            cardFactory: c => c.Create(
                usesTobacco: Context.GetValue<bool?>("uses_tobacco"),
                selectedConditions: Context.GetValue<List<string>>("selected_conditions"),
                hasHealthInsurance: Context.GetValue<bool?>("has_health_insurance"),
                height: Context.GetValue<string>("height"),
                weight: Context.GetValue<string>("weight")
            )
        ));

        var healthQuery = new SemanticQueryActivity<CombinedInsuranceRuleSet, LeadSummaryModel, QualifiedCarriers>(
            id: "HealthInfoQuery",
            kernel: _kernel,
            logger: _logger,
            ruleSet: activeRuleSets,
            inputFactory: () => LeadSummaryBuilder.FromContext(Context),
            outputGuidelinesPrompt: "Include rationale for matchScore and underwriting classification.",
            runInBackground: true
        );

        // health_info_submitted
        healthQuery.OnAsyncCompleted(
            AttachQueryPayloadAsync(
                eventName: "health_info_submitted",
                progress: 55,
                message: "Health information collected",
                queryOutputKey: "output_query_healthinfoquery"
            )
        );

        Add(healthQuery);

        // === ASSETS & LIABILITIES ===
        Add(new AdaptiveCardActivity<AssetsLiabilitiesCard, AssetsLiabilitiesModel>(
            "AssetsLiabilities",
            Context,
            cardFactory: c => c.Create(
                hasHomeEquity: Context.GetValue<string>("has_home_equity"),
                homeEquityAmount: Context.GetValue<string>("home_equity_amount"),
                mortgageDebt: Context.GetValue<string>("mortgage_debt"),
                otherDebt: Context.GetValue<string>("other_debt"),
                savingsAmount: Context.GetValue<string>("savings_amount"),
                investmentsAmount: Context.GetValue<string>("investments_amount"),
                retirementAmount: Context.GetValue<string>("retirement_amount")
            )
        ));

        // assets_liabilities_submitted
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "assets_liabilities_submitted",
            data: new {
                stage = "assets-liabilities",
                progress = 60,
                message = "Assets and liabilities information collected"
            },
            logger: _logger,
            conversationContext: _conversationContext
        ));


        // === COVERAGE INTENT ===
        Add(new AdaptiveCardActivity<CoverageIntentCard, CoverageIntentModel>(
            "CoverageIntent",
            Context,
            cardFactory: c => c.Create(
                selectedCoverageTypes: Context.GetValue<List<string>>("selected_coverage_types"),
                coverageStartTime: Context.GetValue<string>("coverage_start_time"),
                desiredCoverageAmount: Context.GetValue<string>("coverage_amount"),
                monthlyBudget: Context.GetValue<string>("monthly_budget")
            )
        ));

        var coverageQuery = new SemanticQueryActivity<
            CombinedInsuranceRuleSet,
            LeadSummaryModel,
            QualifiedCarriers>(
                id: "CoverageIntentQuery",
                kernel: _kernel,
                logger: _logger,
                ruleSet: activeRuleSets,
                inputFactory: () => LeadSummaryBuilder.FromContext(Context),
                outputGuidelinesPrompt: "Evaluate product fit and show ranked coverage options.",
                runInBackground: true
            );

        // coverage_intent_submitted
        coverageQuery.OnAsyncCompleted(
            AttachQueryPayloadAsync(
                eventName: "coverage_intent_submitted",
                progress: 65,
                message: "Coverage intent captured",
                queryOutputKey: "output_query_coverageintentquery"
            )
        );

        // Finally, add it to the workflow
        Add(coverageQuery);

        // === DEPENDENTS ===
        Add(new AdaptiveCardActivity<DependentsCard, DependentsModel>(
            "Dependents",
            Context,
            cardFactory: c => {
                // -----------------------------
                // Parse "yes/no" → bool?
                // -----------------------------
                bool? hasDeps = Context.GetValue<string>("has_dependents")?.ToLower() switch {
                    "yes" => true,
                    "no" => false,
                    _ => null
                };

                // -----------------------------
                // Selected age ranges (toggles)
                // -----------------------------
                var ranges = new List<string>();

                if (Context.GetValue<string>("ageRange_0_5") == "true") ranges.Add("0-5");
                if (Context.GetValue<string>("ageRange_6_12") == "true") ranges.Add("6-12");
                if (Context.GetValue<string>("ageRange_13_17") == "true") ranges.Add("13-17");
                if (Context.GetValue<string>("ageRange_18_25") == "true") ranges.Add("18-25");
                if (Context.GetValue<string>("ageRange_25plus") == "true") ranges.Add("Over 25");

                // -----------------------------
                // Return card with NEW field: no_of_children
                // -----------------------------
                return c.Create(
                    maritalStatus: Context.GetValue<string>("marital_status"),
                    noOfChildren: Context.GetValue<string>("no_of_children"),   // ← ADDED
                    hasDependents: hasDeps,
                    selectedAgeRanges: ranges
                );
            }
        ));

        var dependentsQuery = new SemanticQueryActivity<
            CombinedInsuranceRuleSet,
            LeadSummaryModel,
            QualifiedCarriers>(
                id: "DependentsQuery",
                kernel: _kernel,
                logger: _logger,
                ruleSet: activeRuleSets,
                inputFactory: () => LeadSummaryBuilder.FromContext(Context),
                outputGuidelinesPrompt: "Assess family needs and dependent coverage gaps.",
                runInBackground: true
            );

        // dependents_submitted
        dependentsQuery.OnAsyncCompleted(
            AttachQueryPayloadAsync(
                eventName: "dependents_submitted",
                progress: 75,
                message: "Dependents data saved",
                queryOutputKey: "output_query_dependentsquery"
            )
        );

        Add(dependentsQuery);

        // === EMPLOYMENT ===
        Add(new AdaptiveCardActivity<EmploymentCard, EmploymentModel>(
            "Employment", Context,
            cardFactory: c => c.Create(
                employmentStatus: Context.GetValue<string>("employment_status"),
                householdIncomeBand: Context.GetValue<string>("household_income"),
                occupation: Context.GetValue<string>("occupation"),
                yearsEmployed: Context.GetValue<string>("years_employed")
            )
        ));

        // employment_submitted
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "employment_submitted",
            data: new { stage = "employment", progress = 85, message = "Employment details collected" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        // === BENEFICIARIES ===
        Add(new AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>(
            "Beneficiaries", Context,
            cardFactory: c => c.Create(
                name: Context.GetValue<string>("beneficiary_name"),
                relation: Context.GetValue<string>("beneficiary_relation"),
                dob: Context.GetValue<string>("beneficiary_dob"),
                percentage: Context.GetValue<int>("beneficiary_percentage")
            )
        ));

        // beneficiaries_submitted (fire immediately after card submission)
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "beneficiaries_submitted",
            data: new { stage = "beneficiaries", progress = 87, message = "Beneficiary information collected" },
            logger: _logger,
            conversationContext: _conversationContext
        ));

        var finalQuery = new SemanticQueryActivity<
            CombinedInsuranceRuleSet,
            LeadSummaryModel,
            QualifiedCarriers>(
                id: "FinalQualificationQuery",
                kernel: _kernel,
                logger: _logger,
                ruleSet: activeRuleSets,
                inputFactory: () => LeadSummaryBuilder.FromContext(Context),
                outputGuidelinesPrompt: "Produce final ranked list of qualifying products with estimated premiums.",
                runInBackground: true
            );

        // beneficiaries_submitted
        finalQuery.OnAsyncCompleted(
            AttachQueryPayloadAsync(
                eventName: "qualification_complete",
                progress: 90,
                message: "Final qualification completed",
                queryOutputKey: "output_query_finalqualificationquery"
            )
        );

        Add(finalQuery);



        // === SUMMARY ===
        Add(new SimpleActivity("T1Summary", (ctx, _) =>
        {
            var name = ctx.GetValue<string>("lead_name") ?? "Unknown";
            var summary = $"✅ Summary ready for {name}";
            ctx.SetValue("marketing_t1_summary", summary);
            return Task.FromResult<object?>(summary);
        }));

        // qualification_complete
        AttachQueryPayload(
            eventName: "qualification_complete",
            progress: 100,
            message: "Qualification process completed",
            queryOutputKey: "output_query_finalqualificationquery"
        );

        _logger.LogInformation("[MarketingT1Topic] ✅ Initialized full flow with semantic reasoning checkpoints.");
    }


    /// <summary>
    /// Utility method to attach a SemanticQuery result as a payload
    /// and fire the corresponding UI event.
    /// </summary>
    private void AttachQueryPayload(
        string eventName,
        int progress,
        string message,
        string queryOutputKey) {
        Add(new EventTriggerActivity(
            id: $"{eventName}_trigger",
            eventName: eventName,
            eventData: new Lazy<object?>(() => {
                // ⚙️ Deferred evaluation of payload
                var payload = Context.GetValue<QualifiedCarriers>(queryOutputKey);
                return new {
                    stage = eventName.Replace("_submitted", string.Empty).Replace('_', '-'),
                    progress,
                    message,
                    payload
                };
            }),
            waitForResponse: false,
            logger: _logger,
            conversationContext: _conversationContext
        ));
    }

    public static Func<TopicWorkflowContext, Task<TopicFlowActivity?>> AttachQueryPayloadAsync(
    string eventName,
    int progress,
    string message,
    string queryOutputKey,
    ILogger? logger = null,
    IConversationContext? conversationContext = null) {
        return async ctx =>
        {
            try {
                // Retrieve payload safely
                var payload = ctx.GetValue<object>(queryOutputKey);

                var eventData = new Lazy<object?>(() => new
                {
                    stage = eventName.Replace("_submitted", string.Empty).Replace('_', '-'),
                    progress,
                    message,
                    payload
                });

                // 🏗 Build but DO NOT execute the custom event trigger
                var eventTrigger = new EventTriggerActivity(
                    id: $"{eventName}_trigger",
                    eventName: eventName,
                    eventData: eventData,
                    waitForResponse: false,
                    logger: logger ?? NullLogger<EventTriggerActivity>.Instance,
                    conversationContext: conversationContext
                );

                logger?.LogInformation(
                    "[AttachQueryPayloadAsync] 🧩 Created EventTriggerActivity for {EventName} (deferred execution)",
                    eventName
                );

                // 🧾 Optionally track metadata for diagnostics
                ctx.SetValue($"{eventName}_progress", progress);
                ctx.SetValue($"{eventName}_message", message);
                ctx.SetValue($"{eventName}_timestamp", DateTime.UtcNow.ToString("o"));

                // ✅ Return the constructed (but unexecuted) activity
                return eventTrigger;
            } catch (Exception ex) {
                logger?.LogError(
                    ex,
                    "[AttachQueryPayloadAsync] ❌ Failed to build event trigger for {EventName}",
                    eventName
                );

                // Return null to indicate failure (nothing to run)
                return null;
            }
        };
    }


    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
        var msg = message.ToLowerInvariant();
        var matches = IntentKeywords.Count(msg.Contains);
        var confidence = matches > 0 ? Math.Min(1.0f, matches / 3.0f) : 0f;

        _logger.LogDebug("[MarketingT1Topic] Intent confidence {Confidence} for message '{Message}'", confidence, message);
        return Task.FromResult(confidence);
    }

    public override async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        Context.SetValue("MarketingT1Topic_runasync", DateTime.UtcNow.ToString("o"));
        var result = await base.RunAsync(cancellationToken);
        result.IsCompleted = true;
        return result;
    }
}
