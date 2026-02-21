using ConversaCore.TopicFlow;
using InsuranceLeadsAgent.Cards;
using InsuranceLeadsAgent.Models;
using Microsoft.Extensions.Logging;

namespace InsuranceLeadsAgent.Topics {
    public class EligibilityTopic : TopicFlow {
        private readonly ILogger<EligibilityTopic> _logger;

        public EligibilityTopic(
            TopicWorkflowContext context,
            ILogger<EligibilityTopic> logger)
            : base(context, logger, "EligibilityTopic") {
            _logger = logger;
            BuildWorkflow();
        }

        public override void Reset() {
            base.Reset();
            BuildWorkflow();
        }

        private void BuildWorkflow() {
            // Single combined pre-qualification card (age, height, weight, conditions)
            Add(new AdaptiveCardActivity<PreQualificationCard, PreQualificationEligibilityModel>(
                "PreQualification",
                Context,
                cardFactory: c => c.Create()
            ) {
                IsRequired = true
            });

            // CALCULATE RISK
            Add(new SimpleActivity(
                "CalculateRisk",
                (ctx, _) => {
                    // Values populated by BaseCardModel.UpdateContext on the combined model
                    var age = ctx.GetValue<int>("age");

                    var height = ctx.GetValue<double>("height");
                    var weight = ctx.GetValue<double>("weight");
                    var bmi = (weight / (height * height)) * 703;

                    var conditions = ctx.GetValue<List<string>>("conditions");
                    
                    // Handle null or empty conditions list (e.g., when "None of the above" is selected)
                    var hasConditions = conditions != null && conditions.Any();
                    var hasHeartDisease = hasConditions && conditions.Contains("HeartDisease");
                    var hasCancer = hasConditions && conditions.Contains("Cancer");

                    LifeRiskClass risk;

                    if (age > 75)
                        risk = LifeRiskClass.Declined;
                    else if (hasHeartDisease || hasCancer)
                        risk = LifeRiskClass.Substandard;
                    else if (bmi > 32)
                        risk = LifeRiskClass.Standard;
                    else if (age < 40 && bmi < 28)
                        risk = LifeRiskClass.PreferredPlus;
                    else
                        risk = LifeRiskClass.Preferred;

                    ctx.SetValue("life_risk_class", risk);

                    return Task.FromResult<object?>($"Preliminary underwriting result: {risk}");
                }
            ));

            Add(new EndActivity("EligibilityEnd"));
        }
    }
}
