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
using ConversaCore.Tools;
using ConversaCore.Runtime;
using InsuranceAgent.Tools;
using InsuranceAgent.Activities;
using InsuranceAgent.Contracts;
using InsuranceSemanticV2.Core.DTO;

namespace InsuranceAgent.Topics;

/// <summary>
/// MarketingT1Topic handles the full-consent lead qualification sequence (TCPA + CCPA approved).
/// It collects lead details, life goals, and other profile data through adaptive cards.
/// Between cards, it emits *semantic* custom events that the UI can listen to (e.g. CustomerConsole).
/// </summary>
public class MarketingT1Topic : TopicFlow, IAsyncInitializable {
    private readonly ILogger<MarketingT1Topic> _logger;
    private readonly Kernel _kernel;
    private readonly InsuranceRuleRepository _insuranceRuleRepository;
    private readonly IToolExecutor _toolExecutor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConversationOutputDispatcher _outputDispatcher;
    private readonly IConversationSession _conversationSession;

    public static readonly string[] IntentKeywords = new[]
    {
        "full marketing", "type 1", "t1", "complete path",
        "full qualification", "marketing path one", "lead qualification"
    };

    public MarketingT1Topic(
        TopicWorkflowContext context,
        ILogger<MarketingT1Topic> logger,
        Kernel kernel,
        InsuranceRuleRepository insuranceRuleRepository,
        IToolExecutor toolExecutor,
        IServiceProvider serviceProvider,
        IConversationOutputDispatcher outputDispatcher,
        IConversationSession conversationSession)
        : base(context, logger, name: InsuranceTopicIds.MarketingT1) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _insuranceRuleRepository = insuranceRuleRepository ?? throw new ArgumentNullException(nameof(insuranceRuleRepository));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _outputDispatcher = outputDispatcher ?? throw new ArgumentNullException(nameof(outputDispatcher));
        _conversationSession = conversationSession ?? throw new ArgumentNullException(nameof(conversationSession));

        Context.SetValue("TopicName", "Marketing Path Type 1");
        Context.SetValue("marketing_path_type", "T1");
        Context.SetValue("MarketingT1Topic_create", DateTime.UtcNow.ToString("o"));

    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        await InitializeActivitiesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Marketing topic {TopicId} initialization completed", Name);
    }

    private async Task InitializeActivitiesAsync(CancellationToken cancellationToken) {
        ClearActivities();

        // --- Resolve active rule set based on life goals ---
        var activeRuleSets = await RuleSelector.SelectRulesAsync(Context, _insuranceRuleRepository);
        cancellationToken.ThrowIfCancellationRequested();

        // === INITIALIZATION ===
        Add(Notify(
            "ShowCustomerConsole",
            "customer_console_show",
            _ => new InsuranceCustomerConsoleNotification("Displaying customer console for lead qualification")));

        // === LEAD DETAILS ===
        Add(Notify(
            "LeadDetailsStarted",
            "lead_details_started",
            _ => new InsuranceProgressNotification("lead-details", 0, "Collecting lead details")));

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

        Add(new InvokeToolActivity<CreateLeadTool, CreateLeadRequest, CreateLeadResult>(
            "CreateLead",
            "insurance.lead.create",
            _toolExecutor,
            context => new CreateLeadRequest(
                context.GetValue<LeadDetailsModel>("LeadDetailsModel")
                    ?? throw new InvalidOperationException("Lead details are required before lead creation."),
                context.GetValue<ContactInfoModel>("ContactInfoModel")),
            context => new ToolExecutionContext {
                ConversationId = context.GetValue<string>("ConversationId") ?? "insurance",
                Subject = "insurance-host",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _serviceProvider,
                TrustedIdentityValidated = true,
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "insurance.lead.create" }
            },
            "insurance.lead.create.result"));

        Add(ProfileTool<SaveContactInfoTool, SaveContactInfoRequest>(
            "SaveContactInfo", "insurance.profile.contact.save",
            context => new SaveContactInfoRequest(
                GetLeadId(context),
                context.GetValue<ContactInfoModel>("ContactInfoModel")
                    ?? throw new InvalidOperationException("Contact information is required before persistence."))));

        // contact_info_submitted
        Add(Notify("ContactInfoSubmitted", "contact_info_submitted",
            _ => new InsuranceProgressNotification("contact-info", 10, "Contact info verified")));

        // lead_details_submitted
        Add(Notify("LeadDetailsSubmitted", "lead_details_submitted",
            _ => new InsuranceProgressNotification("lead-details", 20, "Lead details collected")));

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

        Add(new InvokeToolActivity<SaveLifeGoalsTool, SaveLifeGoalsRequest, ProfileWriteResult>(
            "SaveLifeGoals",
            "insurance.profile.life-goals.save",
            _toolExecutor,
            context => new SaveLifeGoalsRequest(
                context.GetValue<ToolResult<CreateLeadResult>>("insurance.lead.create.result")?.Value?.LeadId
                    ?? throw new InvalidOperationException("A created lead is required before profile persistence."),
                context.GetValue<LifeGoalsModel>("LifeGoalsModel")
                    ?? throw new InvalidOperationException("Life goals are required before persistence.")),
            _ => new ToolExecutionContext {
                ConversationId = "insurance",
                Subject = "insurance-host",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _serviceProvider,
                TrustedIdentityValidated = true,
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "insurance.profile.life-goals.save" }
            },
            "insurance.profile.life-goals.save.result"));

        // life_goals_submitted
        Add(Notify("LifeGoalsSubmitted", "life_goals_submitted",
            _ => new InsuranceProgressNotification("life-goals", 40, "Life goals recorded")));

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

        Add(ProfileTool<SaveHealthInfoTool, SaveHealthInfoRequest>(
            "SaveHealthInfo", "insurance.profile.health.save",
            context => new SaveHealthInfoRequest(
                GetLeadId(context),
                context.GetValue<HealthInfoModel>("HealthInfoModel")
                    ?? throw new InvalidOperationException("Health information is required before persistence."))));

        var healthQuery = new SemanticQueryActivity<CombinedInsuranceRuleSet, LeadSummaryModel, QualifiedCarriers>(
            id: "HealthInfoQuery",
            kernel: _kernel,
            logger: _logger,
            ruleSet: activeRuleSets,
            inputFactory: () => LeadSummaryBuilder.FromContext(Context),
            outputGuidelinesPrompt: "Include rationale for matchScore and underwriting classification.",
            runInBackground: false
        );

        Add(healthQuery);
        AttachQueryPayload(
            eventName: "health_info_submitted",
            progress: 55,
            message: "Health information collected",
            queryOutputKey: "output_query_healthinfoquery");

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
        Add(Notify("AssetsLiabilitiesSubmitted", "assets_liabilities_submitted",
            _ => new InsuranceProgressNotification(
                "assets-liabilities", 60, "Assets and liabilities information collected")));


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

        Add(ProfileTool<SaveCoverageIntentTool, SaveCoverageIntentRequest>(
            "SaveCoverageIntent", "insurance.profile.coverage.save",
            context => new SaveCoverageIntentRequest(
                GetLeadId(context),
                context.GetValue<CoverageIntentModel>("CoverageIntentModel")
                    ?? throw new InvalidOperationException("Coverage intent is required before persistence."))));

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
                runInBackground: false
            );

        Add(coverageQuery);
        AttachQueryPayload(
            eventName: "coverage_intent_submitted",
            progress: 65,
            message: "Coverage intent captured",
            queryOutputKey: "output_query_coverageintentquery");

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

        Add(ProfileTool<SaveDependentsTool, SaveDependentsRequest>(
            "SaveDependents", "insurance.profile.dependents.save",
            context => new SaveDependentsRequest(
                GetLeadId(context),
                context.GetValue<DependentsModel>("DependentsModel")
                    ?? throw new InvalidOperationException("Dependents are required before persistence."))));

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
                runInBackground: false
            );

        Add(dependentsQuery);
        AttachQueryPayload(
            eventName: "dependents_submitted",
            progress: 75,
            message: "Dependents data saved",
            queryOutputKey: "output_query_dependentsquery");

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

        Add(ProfileTool<SaveEmploymentTool, SaveEmploymentRequest>(
            "SaveEmployment", "insurance.profile.employment.save",
            context => new SaveEmploymentRequest(
                GetLeadId(context),
                context.GetValue<EmploymentModel>("EmploymentModel")
                    ?? throw new InvalidOperationException("Employment information is required before persistence."))));

        // employment_submitted
        Add(Notify("EmploymentSubmitted", "employment_submitted",
            _ => new InsuranceProgressNotification("employment", 85, "Employment details collected")));

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

        Add(ProfileTool<SaveBeneficiariesTool, SaveBeneficiariesRequest>(
            "SaveBeneficiaries", "insurance.profile.beneficiaries.save",
            context => new SaveBeneficiariesRequest(
                GetLeadId(context),
                context.GetValue<BeneficiaryInfoModel>("BeneficiaryInfoModel")
                    ?? throw new InvalidOperationException("Beneficiary information is required before persistence."))));

        // beneficiaries_submitted (fire immediately after card submission)
        Add(Notify("BeneficiariesSubmitted", "beneficiaries_submitted",
            _ => new InsuranceProgressNotification("beneficiaries", 87, "Beneficiary information collected")));

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
                runInBackground: false
        );

        Add(finalQuery);

        Add(ConditionalActivity<TopicFlowActivity>.If(
            "QualifiedLeadHandoffDecision",
            context => GetQualificationScore(context) is >= 70,
            (_, _) => QualifiedLeadHandoff(),
            (_, _) => new SimpleActivity(
                "SkipQualifiedLeadHandoff",
                "Qualification did not meet the human-agent handoff threshold."),
            _logger));



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

    private InvokeToolActivity<TTool, TRequest, ProfileWriteResult> ProfileTool<TTool, TRequest>(
        string activityId,
        string toolId,
        Func<TopicWorkflowContext, TRequest> requestFactory)
        where TTool : class, IConversaTool<TRequest, ProfileWriteResult>
    {
        return new InvokeToolActivity<TTool, TRequest, ProfileWriteResult>(
            activityId,
            toolId,
            _toolExecutor,
            requestFactory,
            _ => new ToolExecutionContext {
                ConversationId = "insurance",
                Subject = "insurance-host",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _serviceProvider,
                TrustedIdentityValidated = true,
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { toolId }
            },
            $"{toolId}.result");
    }

    private InsuranceHostNotificationActivity<TPayload> Notify<TPayload>(
        string activityId,
        string eventName,
        Func<TopicWorkflowContext, TPayload> payloadFactory)
        where TPayload : notnull =>
        new(activityId, eventName, payloadFactory, _outputDispatcher, _conversationSession);

    private static int GetLeadId(TopicWorkflowContext context) =>
        context.GetValue<ToolResult<CreateLeadResult>>("insurance.lead.create.result")?.Value?.LeadId
        ?? throw new InvalidOperationException("A created lead is required before profile persistence.");

    private TopicFlowActivity QualifiedLeadHandoff() =>
        new InvokeToolActivity<QualifiedLeadHandoffTool, QualifiedLeadHandoffRequest, QualifiedLeadHandoffResponse>(
            "HandoffQualifiedLead",
            "insurance.lead.handoff",
            _toolExecutor,
            context => new QualifiedLeadHandoffRequest(GetLeadId(context), GetQualificationScore(context)),
            context => new ToolExecutionContext {
                ConversationId = context.GetValue<string>("ConversationId") ?? "insurance",
                Subject = "insurance-host",
                CorrelationId = Guid.NewGuid().ToString("N"),
                Services = _serviceProvider,
                TrustedIdentityValidated = true,
                AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "insurance.lead.handoff" }
            },
            "insurance.lead.handoff.result");

    private static int? GetQualificationScore(TopicWorkflowContext context) {
        var scores = context
            .GetValue<QualifiedCarriers>("output_query_finalqualificationquery")?
            .Carriers
            .Where(carrier => carrier.MatchScore.HasValue)
            .Select(carrier => carrier.MatchScore!.Value)
            .ToArray();
        return scores is { Length: > 0 } ? scores.Max() : null;
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
        var stage = eventName.Replace("_submitted", string.Empty).Replace('_', '-');
        TopicFlowActivity activity = eventName == "qualification_complete"
            ? Notify($"{eventName}_notification", eventName, context =>
                new InsuranceQualificationNotification(
                    stage, progress, message, context.GetValue<QualifiedCarriers>(queryOutputKey)))
            : Notify($"{eventName}_notification", eventName, context =>
                new InsuranceProgressNotification(
                    stage, progress, message,
                    Payload: context.GetValue<QualifiedCarriers>(queryOutputKey)));
        Add(activity);
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
