using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;
using InsuranceLeadsAgent.Models;

namespace InsuranceLeadsAgent.Topics
{
    public class LeadOrchestratorTopic : TopicFlow
    {
        private readonly ILogger<LeadOrchestratorTopic> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public LeadOrchestratorTopic(
            TopicWorkflowContext context,
            ILogger<LeadOrchestratorTopic> logger,
            ILoggerFactory loggerFactory)
            : base(context, logger, "LeadOrchestrator")
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            BuildWorkflow();
        }

        public override void Reset()
        {
            base.Reset();
            BuildWorkflow();
        }

        private void BuildWorkflow()
        {
            // Opening message
            Add(new SimpleActivity(
                "OpeningMessage",
                "I can check which coverage programs you qualify for. It takes about 2–3 minutes."
            ));

            // Core filters placeholder - will be replaced with actual card later
            Add(new SimpleActivity(
                "CoreFiltersPlaceholder",
                "Let me gather some basic information to check your eligibility..."
            ));

            Add(new TriggerTopicActivity(
                "TriggerEligibility",
                "EligibilityTopic",
                waitForCompletion: true,
                logger: _logger
            ));

            Add(new SimpleActivity(
                "CheckQualificationTier",
                (ctx, _) =>
                {
                    var decision = ctx.GetValue<QualificationDecision>("qualification_decision");
                    if (decision != null && decision.HighestTier != QualificationTier.None)
                    {
                        return Task.FromResult<object?>("Good news — you qualify for coverage programs.");
                    }
                    else
                    {
                        return Task.FromResult<object?>("You may qualify for limited or modified coverage options.");
                    }
                }
            ));

           Add(new QuickAnswerActivity(
                "MoveForwardPrompt",
                "Would you like to move forward with next steps?",
                new[]
                {
                    "Yes",
                    "Not right now"
                },
                Context,
                _loggerFactory.CreateLogger<QuickAnswerActivity>()
            ));

            Add(new SimpleActivity(
                "CheckAndRouteResponse",
                (ctx, _) =>
                {
                    var response = ctx.GetModelProperty<string>("MoveForwardPrompt", "answer", string.Empty);
                    
                    if (response == "Yes")
                    {
                        ctx.SetValue("should_proceed_to_intake", true);
                        return Task.FromResult<object?>(null);
                    }
                    else
                    {
                        ctx.SetValue("should_proceed_to_intake", false);
                        return Task.FromResult<object?>("No problem. If you'd like to continue later, just let me know.");
                    }
                }
            ));

            Add(new TriggerTopicActivity(
                "TriggerIntake",
                "Intake",
                waitForCompletion: true,
                logger: _logger
            ));

            Add(new TriggerTopicActivity(
                "TriggerTransfer",
                "Transfer",
                waitForCompletion: true,
                logger: _logger
            ));

            // === REPORT GENERATION ===

            Add(new SimpleActivity(
                "GenerateLeadReport",
                (ctx, _) =>
                {
                    var report = LeadProspectReportBuilder.Build(ctx);
                    var fileName = $"LeadReport_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                    ctx.SetValue("pdf_path", $"/reports/{fileName}");
                    ctx.SetValue("lead_report", report);
                    return Task.FromResult<object?>($"PDF report generated: {fileName}");
                }
            ));

            // === EMAIL TO AGENT ===

            Add(new SimpleActivity(
                "SendLeadEmail",
                (ctx, _) =>
                {
                    var pdfPath = ctx.GetValue<string>("pdf_path");
                    var emailTo = "agent@company.com";
                    var subject = "New Insurance Lead";
                    var body = "A new lead has completed the qualification flow. See attached report.";
                    
                    // Simulate email send
                    ctx.SetValue("email_sent", true);
                    ctx.SetValue("email_details", new { to = emailTo, subject, body, attachment = pdfPath });
                    return Task.FromResult<object?>($"Email sent to {emailTo}: {subject}");
                }
            ));

            // === CRM WEBHOOK ===

            Add(new SimpleActivity(
                "PushLeadToCRM",
                (ctx, _) =>
                {
                    var payload = new
                    {
                        fullName = ctx.GetValue<string>("full_name") ?? "Not provided",
                        state = ctx.GetValue<string>("state") ?? "Not provided",
                        tier = ctx.GetValue<QualificationDecision>("qualification_decision")?.HighestTier.ToString() ?? "None",
                        intakeCompleted = ctx.GetValue<bool?>("intake_completed") ?? false,
                        transferSuccessful = ctx.GetValue<bool?>("transfer_successful") ?? false
                    };
                    
                    var webhookUrl = "https://hooks.zapier.com/your-webhook-url";
                    
                    // Simulate webhook push
                    ctx.SetValue("crm_webhook_sent", true);
                    return Task.FromResult<object?>($"Webhook sent to {webhookUrl}");
                }
            ));

            // === END ===

            Add(new EndActivity("LeadOrchestratorEnd"));
        }
    }
}



