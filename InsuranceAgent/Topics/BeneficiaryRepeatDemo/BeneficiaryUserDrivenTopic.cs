using ConversaCore.Context;
using ConversaCore.TopicFlow;
using InsuranceAgent.Cards;

namespace InsuranceAgent.Topics.BeneficiaryRepeatDemo;

/// <summary>
/// Enhanced beneficiary collection topic with user-driven continuation.
/// Shows progress indicators and asks users if they want to add more.
/// </summary>
public class BeneficiaryUserDrivenTopic : TopicFlow
{
    private readonly ILogger<BeneficiaryUserDrivenTopic> _logger;
    private readonly IConversationContext? _conversationContext;
    private readonly List<BeneficiaryInfoModel> _collectedBeneficiaries = new();
    private int _currentCount = 0;

    // Intent recognition keywords
    public static readonly string[] IntentKeywords = new[] { 
        "user", "driven", "flexible", "continuation", "enhanced", "better"
    };

    public BeneficiaryUserDrivenTopic(
        TopicWorkflowContext context, 
        ILogger<BeneficiaryUserDrivenTopic> logger,
        IConversationContext? conversationContext = null) : base(context, logger, name: "BeneficiaryUserDrivenTopic")
    {
        _logger = logger;
        _conversationContext = conversationContext;

        _logger.LogWarning("[BeneficiaryUserDrivenTopic] *** CONSTRUCTOR CALLED - User-driven demo starting ***");

        // Start with the first beneficiary
        AddNextBeneficiaryCard();
    }

    /// <summary>
    /// Adds the next beneficiary collection card
    /// </summary>
    private void AddNextBeneficiaryCard()
    {
        _currentCount++;
        var activityId = $"Beneficiary_{_currentCount}";
        
        var beneficiaryActivity = new AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>(
            id: activityId,
            context: Context,
            cardFactory: card => card.Create(
                progressText: $"Beneficiary #{_currentCount}"
            ),
            modelContextKey: $"BeneficiaryModel_{_currentCount}"
        );

        // Handle successful completion
        beneficiaryActivity.ModelBound += (s, e) => {
            var model = (BeneficiaryInfoModel)e.Model;
            _collectedBeneficiaries.Add(model);
            
            _logger.LogInformation("[BeneficiaryUserDriven] Added beneficiary #{Count}: {Name}", 
                _currentCount, model.BeneficiaryName);
            
            // After first beneficiary, show continuation card
            if (_currentCount >= 1)
            {
                AddContinuationCard();
            }
        };

        Add(beneficiaryActivity);
    }

    /// <summary>
    /// Adds a continuation card asking if user wants to add another beneficiary
    /// </summary>
    private void AddContinuationCard()
    {
        var activityId = $"Continue_{_currentCount}";
        
        // Build summary of current beneficiaries
        var summary = "";
        for (int i = 0; i < _collectedBeneficiaries.Count; i++) {
            var ben = _collectedBeneficiaries[i];
            summary += $"{i + 1}. {ben.BeneficiaryName} ({ben.BeneficiaryRelationship}) - {ben.BeneficiaryPercentage}%\n";
        }

        var continueActivity = new AdaptiveCardActivity<ContinuationCard, InsuranceAgent.Cards.ContinuationModel>(
            id: activityId,
            context: Context,
            cardFactory: card => card.Create(
                currentCount: _collectedBeneficiaries.Count,
                itemType: "beneficiary",
                currentSummary: summary,
                promptText: _collectedBeneficiaries.Count == 1 ? 
                    "You've added 1 beneficiary. Would you like to add another?" :
                    $"You've added {_collectedBeneficiaries.Count} beneficiaries. Would you like to add another?"
            ),
            modelContextKey: $"ContinuationModel_{_currentCount}",
            customMessage: $"🎯 You've successfully added {_collectedBeneficiaries.Count} beneficiar{(_collectedBeneficiaries.Count == 1 ? "y" : "ies")}! What would you like to do next?"
        );

        // Handle continuation decision
        continueActivity.ModelBound += (s, e) => {
            var model = (InsuranceAgent.Cards.ContinuationModel)e.Model;
            _logger.LogWarning("[BeneficiaryUserDriven] *** CONTINUATION CARD MODEL BOUND: {Choice} ***", model.ContinueChoice);
            
            if (model.ShouldContinue)
            {
                _logger.LogInformation("[BeneficiaryUserDriven] User chose to add another beneficiary");
                AddNextBeneficiaryCard();
            }
            else
            {
                _logger.LogInformation("[BeneficiaryUserDriven] User chose to finish. Adding summary.");
                AddSummaryActivity();
            }
        };

        _logger.LogWarning("[BeneficiaryUserDriven] *** ADDING CONTINUATION CARD TO QUEUE: {ActivityId} ***", activityId);
        Add(continueActivity);
    }

    /// <summary>
    /// Adds the final summary activity
    /// </summary>
    private void AddSummaryActivity()
    {
        Add(new SimpleActivity("ShowSummary", (context, input) =>
        {
            var summary = "🎉 **Beneficiary Collection Complete!**\n\n";
            summary += $"📊 **Total Beneficiaries Added:** {_collectedBeneficiaries.Count}\n\n";
            
            for (int i = 0; i < _collectedBeneficiaries.Count; i++) {
                var beneficiary = _collectedBeneficiaries[i];
                summary += $"**{i + 1}. {beneficiary.BeneficiaryName}**\n";
                summary += $"   - Relationship: {beneficiary.BeneficiaryRelationship}\n";
                summary += $"   - Percentage: {beneficiary.BeneficiaryPercentage}%\n";
                if (beneficiary.BeneficiaryDob.HasValue)
                    summary += $"   - Date of Birth: {beneficiary.BeneficiaryDob:yyyy-MM-dd}\n";
                summary += "\n";
            }

            _logger.LogInformation("[BeneficiaryUserDriven] Summary generated for {Count} beneficiaries", _collectedBeneficiaries.Count);
            return Task.FromResult<object?>(ActivityResult.Continue(summary));
        }));
    }

    public override Task<float> CanHandleAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[BeneficiaryUserDrivenTopic] *** CanHandleAsync called with message: '{Message}' ***", message);

        var keywords = new[] { "user", "driven", "flexible", "continuation", "enhanced", "better" };
        var isMatch = keywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        if (isMatch)
        {
            _logger.LogWarning("[BeneficiaryUserDrivenTopic] *** HIGH CONFIDENCE MATCH - returning 1.0 ***");
            return Task.FromResult(1.0f);
        }

        return Task.FromResult(0.0f);
    }


}