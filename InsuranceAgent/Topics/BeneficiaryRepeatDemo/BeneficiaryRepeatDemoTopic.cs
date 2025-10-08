using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics.BeneficiaryInfoTopic;

namespace InsuranceAgent.Topics.BeneficiaryRepeatDemo
{
    public class BeneficiaryRepeatDemoTopic : TopicFlow
    {
        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] { "repeat", "demo", "beneficiary", "multiple", "embedded" };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<BeneficiaryRepeatDemoTopic> _logger;

        public BeneficiaryRepeatDemoTopic(
            TopicWorkflowContext workflowContext,
            ILogger<BeneficiaryRepeatDemoTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext)
            : base(workflowContext, logger, name: "BeneficiaryRepeatDemoTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            _logger.LogWarning("[BeneficiaryRepeatDemoTopic] *** CONSTRUCTOR CALLED - RepeatActivity embedded continuation demo starting ***");
            Console.WriteLine("*** BeneficiaryRepeatDemoTopic CONSTRUCTOR CALLED ***");

            // Create a RepeatActivity with embedded continuation prompts (UserPrompted mode)
            var repeatActivity = RepeatActivity.UserPrompted<AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>>(
                id: "CollectBeneficiaries",
                activityFactory: (iterationId, context) =>
                {
                    _logger.LogInformation("[BeneficiaryRepeatDemoTopic] Creating beneficiary card activity for iteration: {IterationId}", iterationId);
                    
                    // Extract iteration number from ID for progress display
                    var iterationNumber = iterationId.Contains("Iteration") ? 
                        iterationId.Substring(iterationId.LastIndexOf("Iteration") + 9) : "1";

                    
                    return new AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>(
                        id: iterationId,
                        context: context,
                        cardFactory: card => card.Create(progressText: $"Beneficiary #{iterationNumber}"),
                        modelContextKey: $"Beneficiary_{iterationId}",
                        onTransition: (from, to, data) => {
                            var stamp = DateTime.UtcNow.ToString("o");
                            _logger.LogInformation("[BeneficiaryRepeatActivity] {ActivityId}: {From} → {To} @ {Stamp} | Data={DataType}", 
                                iterationId, from, to, stamp, data?.GetType().Name ?? "null");
                        }
                    );
                },
                continuePrompt: "Would you like to add another beneficiary to your policy?",
                logger: _logger
            );

            Add(repeatActivity);

            // Add a summary activity to show collected results
            Add(new SimpleActivity("ShowSummary", (context, input) =>
            {
                var collectedResults = repeatActivity.GetCollectedResults();
                _logger.LogInformation("[BeneficiaryRepeatDemoTopic] Collected {Count} beneficiaries", collectedResults.Count);
                
                var summary = "✅ **Beneficiaries Collected:**\n\n";
                for (int i = 0; i < collectedResults.Count; i++)
                {
                    if (collectedResults[i] is BeneficiaryInfoModel beneficiary)
                    {
                        summary += $"{i + 1}. {beneficiary.BeneficiaryName} ({beneficiary.BeneficiaryRelationship}) - {beneficiary.BeneficiaryPercentage}%\n";
                    }
                }
                
                return Task.FromResult<object?>(summary);
            }));
        }

        public override Task<float> CanHandleAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("[BeneficiaryRepeatDemoTopic] *** CanHandleAsync called with message: '{Message}' ***", message);
            Console.WriteLine($"*** BeneficiaryRepeatDemoTopic.CanHandleAsync called with: '{message}' ***");
            
            if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
            var normalized = message.ToLowerInvariant();
            
            // High confidence for exact matches
            if (normalized.Contains("repeat demo") || 
                normalized.Contains("beneficiary repeat") || 
                normalized.Contains("embedded continuation") ||
                normalized.Contains("embedded demo"))
            {
                _logger.LogWarning("[BeneficiaryRepeatDemoTopic] *** HIGH CONFIDENCE MATCH - returning 1.0 ***");
                Console.WriteLine("*** HIGH CONFIDENCE MATCH - returning 1.0 ***");
                return Task.FromResult(1.0f);
            }
                
            // Medium confidence for partial matches
            if (IntentKeywords.Any(keyword => normalized.Contains(keyword.ToLowerInvariant())))
            {
                _logger.LogWarning("[BeneficiaryRepeatDemoTopic] *** MEDIUM CONFIDENCE MATCH - returning 0.6 ***");
                Console.WriteLine("*** MEDIUM CONFIDENCE MATCH - returning 0.6 ***");
                return Task.FromResult(0.6f);
            }
                
            _logger.LogInformation("[BeneficiaryRepeatDemoTopic] No match - returning 0.0");
            return Task.FromResult(0f);
        }
    }
}