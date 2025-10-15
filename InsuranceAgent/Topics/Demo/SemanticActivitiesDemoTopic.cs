using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.TopicFlow.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using InsuranceAgent.Models.Decisions;

namespace InsuranceAgent.Topics.Demo;

/// <summary>
/// Demo topic showcasing the new semantic activities for insurance decision making and conversation redirection.
/// </summary>
public class SemanticActivitiesDemoTopic : TopicFlow
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticActivitiesDemoTopic> _logger;

    public SemanticActivitiesDemoTopic(
        TopicWorkflowContext context,
        ILogger<SemanticActivitiesDemoTopic> logger,
        IConversationContext conversationContext,
        Kernel kernel) : base(context, logger, "SemanticActivitiesDemoTopic")
    {
        _kernel = kernel;
        _logger = logger;
        
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Real demo using actual DecisionActivity with OpenAI
        Add(new SimpleActivity("SetupDemo", "🔧 Setting up test data for OpenAI decision making..."));
        
        Add(new SimpleActivity("PrepareTestData", (ctx, input) => Task.FromResult<object?>(PrepareInsuranceTestData(ctx, input))));
        
        // ACTUAL DecisionActivity call to OpenAI - using manual construction with proper parameters
        Add(new DecisionActivity<LeadInfo, DocumentEvidence, InsuranceDecisionResponse>(
            "RealAIDecision",
            _kernel,
            _logger,
            new LeadInfo(), // We'll set actual data in context
            "TEvidence",
            "You are an expert insurance underwriter. Analyze the lead information and evidence to make insurance recommendations.",
            "Based on this lead data: {0} and evidence: {1}, provide your insurance decision analysis.",
            temperature: 0.3f
        ));
        
        Add(new SimpleActivity("ShowResults", (ctx, input) => Task.FromResult<object?>(DisplayAIResults(ctx, input))));
    }

    private ActivityResult PrepareInsuranceTestData(TopicWorkflowContext context, object? input)
    {
        // Create realistic test data for AI decision using actual model structure
        var leadInfo = new LeadInfo
        {
            Age = 35,
            Gender = "male",
            SmokingStatus = "non-smoker",
            State = "california",
            DesiredCoverage = 500000,
            Employment = "full-time",
            MedicalHistory = new List<MedicalCondition>
            {
                new MedicalCondition { Condition = "diabetes_no_insulin", DiagnosisAge = 32, YearsAgo = 3, Controlled = true },
                new MedicalCondition { Condition = "high_blood_pressure", Controlled = true, Medications = 1 }
            }
        };

        var evidence = new DocumentEvidence
        {
            DocumentType = "Medical Records",
            Content = "Patient has well-controlled diabetes and hypertension. Recent lab work shows good glucose control.",
            DocumentDate = DateTime.Now.AddDays(-5),
            Source = "Primary Care Physician"
        };

        // Set data for DecisionActivity to use
        context.SetValue("TInput", leadInfo);
        context.SetValue("TEvidence", evidence);
        
        return ActivityResult.Continue("✅ Test data prepared - sending to OpenAI for real decision...");
    }

    private ActivityResult DisplayAIResults(TopicWorkflowContext context, object? input)
    {
        // Get the AI decision result
        var decision = context.GetValue<InsuranceDecisionResponse>("RealAIDecision");
        
        if (decision != null)
        {
            var result = $"🤖 **OpenAI Decision Result:**\n" +
                        $"• **Perfect Matches:** {decision.PerfectMatches?.Count ?? 0}\n" +
                        $"• **Near Matches:** {decision.NearMatches?.Count ?? 0}\n" +
                        $"• **Recommendation:** {decision.OverallRecommendation}\n" +
                        $"• **Confidence:** {decision.ConfidenceScore:F2}\n" +
                        $"• **Exclusions:** {string.Join(", ", decision.Exclusions ?? new List<string>())}\n\n" +
                        $"🎉 **Live OpenAI integration working!**";
            
            return ActivityResult.Continue(result);
        }
        
        return ActivityResult.Continue("❌ No AI decision received - check OpenAI connection");
    }

    public override async Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        // Check for semantic activity demo keywords
        var keywords = new string[] { "semantic", "demo", "ai decision", "insurance analysis", "redirect test" };
        
        return keywords.Any(keyword => 
            input.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ? 0.9f : 0.0f;
    }

    public static readonly string[] IntentKeywords = new string[]
    {
        "semantic activities demo",
        "insurance decision demo",
        "ai analysis",
        "semantic test",
        "redirection demo"
    };
}