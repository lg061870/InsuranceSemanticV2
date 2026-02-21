using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Topic that helps the user arrive at a rough coverage estimate
/// by capturing a few key details and then summarizing them in
/// plain language. This is intentionally education-oriented, not
/// a formal quote.
/// </summary>
public class CoverageEstimateTopic : TopicFlow {
    private readonly ILogger<CoverageEstimateTopic> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Kernel _kernel;

    public CoverageEstimateTopic(
        TopicWorkflowContext context,
        ILogger<CoverageEstimateTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory,
        Kernel kernel)
        : base(context, logger, "CoverageEstimateTopic") {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _kernel = kernel;
        BuildWorkflow();
    }

    private void BuildWorkflow() {
        Add(new SimpleActivity(
            "CoverageIntro",
            "Great, let's get a rough sense of what kind of coverage might fit your situation. I'll ask just a few quick questions in plain language.")
        );

        // 1) Basic profile (DependentsCard/DependentsModel - local copy)
        Add(new AdaptiveCardActivity<DependentsCard, DependentsModel>(
            id: "Coverage_ProfileCard",
            context: Context,
            cardFactory: card => card.Create(),
            modelContextKey: "Coverage_ProfileModel",
            logger: _loggerFactory.CreateLogger<AdaptiveCardActivity<DependentsModel>>()));

        Add(new SimpleActivity(
            "CaptureCoverageProfile",
            (ctx, input) => {
                var profile = ctx.GetValue<DependentsModel>("Coverage_ProfileModel");
                if (profile != null) {
                    ctx.SetValue("Coverage_Profile_MaritalStatus", profile.MaritalStatus);
                    ctx.SetValue("Coverage_Profile_HasDependents", profile.HasDependents ?? false);
                    ctx.SetValue("Coverage_Profile_DependentsCount", profile.EstimatedNumberOfDependents);
                    ctx.SetValue("Coverage_Profile_FinancialResponsibility", profile.FinancialResponsibilityLevel);
                }
                return Task.FromResult<object?>(null);
            }));

        // 2) Financial snapshot (AssetsLiabilitiesCard/AssetsLiabilitiesModel - local copy)
        Add(new AdaptiveCardActivity<AssetsLiabilitiesCard, AssetsLiabilitiesModel>(
            id: "Coverage_FinancialCard",
            context: Context,
            cardFactory: card => card.Create(),
            modelContextKey: "Coverage_FinancialModel",
            logger: _loggerFactory.CreateLogger<AdaptiveCardActivity<AssetsLiabilitiesModel>>()));

        Add(new SimpleActivity(
            "CaptureCoverageFinancial",
            (ctx, input) => {
                var fin = ctx.GetValue<AssetsLiabilitiesModel>("Coverage_FinancialModel");
                if (fin != null) {
                    ctx.SetValue("Coverage_Financial_NetWorthCategory", fin.NetWorthCategory);
                    ctx.SetValue("Coverage_Financial_StabilityGrade", fin.FinancialStabilityGrade);
                    ctx.SetValue("Coverage_Financial_AssetScore", fin.TotalAssetScore);
                    ctx.SetValue("Coverage_Financial_DebtScore", fin.TotalDebtScore);
                }
                return Task.FromResult<object?>(null);
            }));

        // 3) Coverage preferences (CoverageIntentCard/CoverageIntentModel - local copy)
        Add(new AdaptiveCardActivity<CoverageIntentCard, CoverageIntentModel>(
            id: "Coverage_PreferencesCard",
            context: Context,
            cardFactory: card => card.Create(),
            modelContextKey: "Coverage_PreferencesModel",
            logger: _loggerFactory.CreateLogger<AdaptiveCardActivity<CoverageIntentModel>>()));

        Add(new SimpleActivity(
            "CaptureCoveragePreferences",
            (ctx, input) => {
                var prefs = ctx.GetValue<CoverageIntentModel>("Coverage_PreferencesModel");
                if (prefs != null) {
                    ctx.SetValue("Coverage_Prefs_TypeBand", string.Join(", ", prefs.SelectedCoverageTypes));
                    ctx.SetValue("Coverage_Prefs_StartTime", prefs.PreferredCoverageStartTime);
                    ctx.SetValue("Coverage_Prefs_AmountBand", prefs.DesiredCoverageAmountBand);
                    ctx.SetValue("Coverage_Prefs_EstimatedAmount", prefs.EstimatedCoverageAmount);
                }
                return Task.FromResult<object?>(null);
            }));

        // 4) Semantic intel over all three answers so downstream topics
        // can reuse structured hints.
        var intelActivity = new SemanticResponseActivity(
                        id: "Coverage_Intel",
                        kernel: _kernel,
                        logger: _loggerFactory.CreateLogger<SemanticResponseActivity>(),
                        vectorDb: null,
                        collectionName: "coverage_estimate_intel")
                .WithDeveloperPrompt(@"You are an information extraction engine for a life insurance coverage estimate.
You will receive free-form text that may include: approximate age, family situation,
number of dependents, income range, major debts, years of protection desired,
and budget hints.

Extract any clearly useful facts into a flat JSON object, for example:
{
    ""Age Approx"": ""mid 40s"",
    ""Dependents"": ""2 kids"",
    ""Income Range"": ""60k-80k"",
    ""Has Mortgage"": ""yes"",
    ""Years To Protect"": ""20"",
    ""Budget"": ""around 100 per month""
}

Use short, human-readable keys. If unsure about a value, omit the key or set it to null.
Respond with JSON only.");

        intelActivity.UserPromptContextKey = null; // we'll stitch multi-part text below
        intelActivity.RequireJsonOutput = true;

        // Provide a combined prompt by using a SimpleActivity wrapper
        Add(new SimpleActivity(
            "BuildCoverageIntelPrompt",
            (ctx, input) => {
                var profile = ctx.GetValue<DependentsModel>("Coverage_ProfileModel");
                var fin = ctx.GetValue<AssetsLiabilitiesModel>("Coverage_FinancialModel");
                var prefs = ctx.GetValue<CoverageIntentModel>("Coverage_PreferencesModel");

                var combined = $"Profile: Marital={profile?.MaritalStatus}, Dependents={profile?.EstimatedNumberOfDependents}, Responsibility={profile?.FinancialResponsibilityLevel}\n" +
                               $"Financial: NetWorth={fin?.NetWorthCategory}, Stability={fin?.FinancialStabilityGrade}\n" +
                               $"Preferences: Types={string.Join(", ", prefs?.SelectedCoverageTypes ?? new List<string>())}, AmountBand={prefs?.DesiredCoverageAmountBand}, MonthlyBudget={prefs?.MonthlyBudget}";

                ctx.SetValue("Coverage_IntelPrompt", combined);
                return Task.FromResult<object?>(null);
            }));

        intelActivity.UserPromptContextKey = "Coverage_IntelPrompt";
        intelActivity.WithDataIntelligence();
        Add(intelActivity);

        // 5) Summarize as an educational coverage estimate
        var summaryPrompt = new PromptActivity(
                "CoverageEstimateSummaryPrompt",
                _kernel,
                _loggerFactory.CreateLogger<PromptActivity>())
            .WithSystemPrompt(@"You are an insurance educator helping a user think about a reasonable life insurance coverage range.
Use only the information in context (age hints, dependents, income, debts, years to protect, budget) plus any basics-intel already captured.

Your job:
- Briefly restate their situation (1 short paragraph).
- Suggest a reasonable coverage range (for example, 8-12x income, or 300k-500k) and a term length (for example, until kids are grown or until retirement age).
- Explain in simple language why that range might make sense.
- Make it clear this is an educational starting point, not a quote or advice.
Keep it concise: 2-3 short paragraphs, in the same language the user has been using.")
            .WithUserPrompt(@"Here is the information we have for this person:

Profile: {context.Coverage_ProfileRaw}
Financial: {context.Coverage_FinancialRaw}
Preferences: {context.Coverage_PreferencesRaw}

Extracted intel keys may also be present in context (e.g. Age Approx, Dependents, Income Range, Years To Protect, Budget).

Based on this, provide an educational coverage estimate summary.");

        summaryPrompt.RequireJsonOutput = false;

        Add(new SimpleActivity(
            "RunCoverageEstimateSummary",
            async (ctx, input) => {
                var result = await summaryPrompt.RunAsync(ctx, null, CancellationToken.None);
                var reply = result.Message?.Trim();
                if (string.IsNullOrWhiteSpace(reply)) {
                    reply = "Based on what you've shared, a common starting point is to think about how many years of income you would want to replace and what big obligations you want covered. You can ask me again with a bit more detail, and I can refine this estimate.";
                }

                // End this topic after showing the estimate; router can decide next step.
                Context.SetValue("CoverageEstimate_ModeActive", false);
                return reply!;
            }));
    }

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        // Mark this topic as active only when it is actually running,
        // so it doesn't steal routing away from the initial topics.
        Context.SetValue("CoverageEstimate_ModeActive", true);
        _logger.LogInformation("[CoverageEstimateTopic] RunAsync starting (State={State})", State);
        return base.RunAsync(cancellationToken);
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default) {
        // Prefer this topic when explicitly active
        if (Context.GetValue<bool>("CoverageEstimate_ModeActive"))
            return Task.FromResult(0.96f);

        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(0.0f);

        // Simple entry keywords if router ever wants to start this directly
        if (input.Contains("coverage estimate", System.StringComparison.OrdinalIgnoreCase) ||
            input.Contains("how much coverage", System.StringComparison.OrdinalIgnoreCase)) {
            return Task.FromResult(0.9f);
        }

        return Task.FromResult(0.0f);
    }
}
