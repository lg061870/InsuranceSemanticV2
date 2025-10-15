using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.TopicFlow.Extensions;
using InsuranceAgent.Models.Decisions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace InsuranceAgent.Topics.Demo;

/// <summary>
/// Demo topic showcasing the new generic DecisionActivity pattern with various input/evidence/response combinations
/// </summary>
public class GenericDecisionDemoTopic : TopicFlow
{
    private readonly Kernel _kernel;
    private readonly ILogger<GenericDecisionDemoTopic> _logger;

    public GenericDecisionDemoTopic(
        TopicWorkflowContext context,
        ILogger<GenericDecisionDemoTopic> logger,
        IConversationContext conversationContext,
        Kernel kernel) : base(context, logger, "GenericDecisionDemoTopic")
    {
        _kernel = kernel;
        _logger = logger;
        
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Example 1: Insurance decision with JSON rules
        Add(new SimpleActivity("SetupInsuranceData", (ctx, input) => Task.FromResult<object?>(SetupInsuranceData(ctx, input))))
            .AddDecision<JsonRulesInput, LeadInfo, InsuranceDecisionResponse>(
                activityId: "AnalyzeInsuranceEligibility",
                kernel: _kernel,
                logger: _logger,
                input: new JsonRulesInput 
                { 
                    RulesFilePath = @"C:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\jsonrules\TERM_INS_RULES.json",
                    Parameters = new Dictionary<string, object> { ["analysisType"] = "comprehensive" }
                },
                evidenceContextKey: "LeadInfo",
                systemPrompt: "You are an expert insurance underwriter. Analyze the lead information against the provided rules to determine carrier eligibility.",
                userPromptTemplate: "Based on the rules provided, analyze this lead: {evidence}. Provide perfect matches and near matches with detailed reasoning.",
                temperature: 0.2f
            )
            .Add(new SimpleActivity("DisplayInsuranceResults", (ctx, input) => Task.FromResult<object?>(DisplayInsuranceResults(ctx, input))))

        // Example 2: Document analysis with vector store
            .Add(new SimpleActivity("SetupDocumentData", (ctx, input) => Task.FromResult<object?>(SetupDocumentData(ctx, input))))
            .AddDecision<VectorStoreInput, DocumentEvidence, DocumentAnalysisResponse>(
                "AnalyzeDocument", 
                _kernel, 
                _logger,
                new VectorStoreInput
                {
                    CollectionName = "documents",
                    SearchQueries = new string[] { "medical", "insurance" },
                    MaxResults = 5
                },
                "DocumentEvidence",
                "You are a document analysis expert. Extract key information and provide insights.",
                "Analyze this document evidence: {evidence}. Extract key fields and provide recommendations.",
                0.4f,
                "gpt-4o-mini"
            )
            .Add(new SimpleActivity("DisplayDocumentResults", (ctx, input) => Task.FromResult<object?>(DisplayDocumentResults(ctx, input))))

        // Example 3: Conversation routing with inline rules
            .Add(new SimpleActivity("SetupConversationData", (ctx, input) => Task.FromResult<object?>(SetupConversationData(ctx, input))))
            .AddDecision<InlineRulesInput, ConversationEvidence, ConversationRoutingResponse>(
                activityId: "RouteConversation",
                kernel: _kernel,
                logger: _logger,
                input: new InlineRulesInput
                {
                    Rules = new List<BusinessRule>
                    {
                        new BusinessRule { Id = "insurance", Name = "Insurance Topic", Description = "Route to insurance discussion" },
                        new BusinessRule { Id = "support", Name = "Support Topic", Description = "Route to customer support" }
                    },
                    Context = "Insurance sales conversation"
                },
                evidenceContextKey: "ConversationEvidence",
                systemPrompt: "You are a conversation router. Determine the best topic to handle the user's input.",
                userPromptTemplate: "Based on the routing rules, analyze this conversation: {evidence}. Recommend the best topic and response.",
                temperature: 0.6f
            )
            .Add(new SimpleActivity("DisplayRoutingResults", (ctx, input) => Task.FromResult<object?>(DisplayRoutingResults(ctx, input))));
    }

    private ActivityResult SetupInsuranceData(TopicWorkflowContext context, object? input)
    {
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
            },
            LegalHistory = new List<LegalIssue>
            {
                new LegalIssue { Issue = "dui_one", YearsAgo = 6 }
            }
        };

        context.SetValue("LeadInfo", leadInfo);
        return ActivityResult.Continue("Insurance lead data prepared for analysis.");
    }

    private ActivityResult SetupDocumentData(TopicWorkflowContext context, object? input)
    {
        var documentEvidence = new DocumentEvidence
        {
            DocumentType = "medical_report",
            Content = "Patient has controlled diabetes and takes medication for blood pressure. No recent complications.",
            ExtractedData = new Dictionary<string, object>
            {
                ["patient_age"] = 35,
                ["conditions"] = new string[] { "diabetes", "hypertension" },
                ["medications"] = 2
            },
            DocumentDate = DateTime.Now.AddDays(-30),
            Source = "Primary Care Physician"
        };

        context.SetValue("DocumentEvidence", documentEvidence);
        return ActivityResult.Continue("Document evidence prepared for analysis.");
    }

    private ActivityResult SetupConversationData(TopicWorkflowContext context, object? input)
    {
        var conversationEvidence = new ConversationEvidence
        {
            UserInput = "I need help with my life insurance policy, it's been denied",
            Intent = "support_request",
            ExtractedEntities = new Dictionary<string, object>
            {
                ["product_type"] = "life_insurance",
                ["issue"] = "denial",
                ["sentiment"] = "frustrated"
            },
            ConversationHistory = new List<string>
            {
                "User: Hello",
                "Agent: Hi, how can I help you today?",
                "User: I need help with my life insurance policy, it's been denied"
            },
            CurrentTopic = "general_inquiry"
        };

        context.SetValue("ConversationEvidence", conversationEvidence);
        return ActivityResult.Continue("Conversation evidence prepared for routing analysis.");
    }

    private ActivityResult DisplayInsuranceResults(TopicWorkflowContext context, object? input)
    {
        var response = context.GetValue<InsuranceDecisionResponse>("AnalyzeInsuranceEligibility_TypedResponse");
        
        if (response != null)
        {
            var result = $@"Insurance Decision Analysis Complete!

Perfect Matches: {response.PerfectMatches.Count}
Near Matches: {response.NearMatches.Count}
Overall Recommendation: {response.OverallRecommendation}
Confidence Score: {response.ConfidenceScore:P}

";
            if (response.PerfectMatches.Any())
            {
                result += "Perfect Matches:\n";
                foreach (var match in response.PerfectMatches)
                {
                    result += $"- {match.CarrierName} ({match.ProductType}): {match.Reasoning}\n";
                }
            }

            return ActivityResult.Continue(result);
        }

        return ActivityResult.Continue("Insurance analysis completed - check raw response for details.");
    }

    private ActivityResult DisplayDocumentResults(TopicWorkflowContext context, object? input)
    {
        var response = context.GetValue<DocumentAnalysisResponse>("AnalyzeDocument_TypedResponse");
        
        if (response != null)
        {
            var result = $@"Document Analysis Complete!

Document Type: {response.DocumentType}
Summary: {response.Summary}
Confidence: {response.ConfidenceScore:P}

Extracted Fields: {response.ExtractedFields.Count}
Key Findings: {string.Join(", ", response.KeyFindings)}
";
            return ActivityResult.Continue(result);
        }

        return ActivityResult.Continue("Document analysis completed - check raw response for details.");
    }

    private ActivityResult DisplayRoutingResults(TopicWorkflowContext context, object? input)
    {
        var response = context.GetValue<ConversationRoutingResponse>("RouteConversation_TypedResponse");
        
        if (response != null)
        {
            var result = $@"Conversation Routing Complete!

Recommended Topic: {response.RecommendedTopic}
Response Message: {response.ResponseMessage}
Confidence: {response.ConfidenceScore:P}

Suggested Actions: {string.Join(", ", response.SuggestedActions)}
";
            return ActivityResult.Continue(result);
        }

        return ActivityResult.Continue("Conversation routing completed - check raw response for details.");
    }

    public override async Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        var keywords = new string[] { "decision demo", "generic decision", "ai decision test", "decision activity" };
        
        return keywords.Any(keyword => 
            input.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ? 0.9f : 0.0f;
    }

    public static readonly string[] IntentKeywords = new string[]
    {
        "decision demo",
        "generic decision",
        "ai decision test",
        "decision activity demo"
    };
}