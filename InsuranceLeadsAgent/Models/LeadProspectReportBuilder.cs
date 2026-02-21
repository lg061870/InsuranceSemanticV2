using ConversaCore.TopicFlow;

namespace InsuranceLeadsAgent.Models
{
    public static class LeadProspectReportBuilder
    {
        public static LeadProspectReportModel Build(TopicWorkflowContext ctx)
        {
            var decision = ctx.GetValue<QualificationDecision>("qualification_decision");
            
            return new LeadProspectReportModel
            {
                FullName = ctx.GetValue<string>("full_name") ?? "Not provided",
                State = ctx.GetValue<string>("state") ?? "Not provided",
                Email = ctx.GetValue<string>("email") ?? "Not provided",
                Phone = ctx.GetValue<string>("phone") ?? "Not provided",
                QualificationTier = decision?.HighestTier.ToString() ?? "None",
                UnderwritingClassification = decision?.UnderwritingClassification ?? "Not assessed",
                ContactMethod = ctx.GetModelProperty<string>("ContactMethodSelection", "answer", "Not selected"),
                IntakeCompleted = ctx.GetValue<bool?>("intake_completed") ?? false,
                TransferAttempted = ctx.GetValue<bool?>("transfer_attempted") ?? false,
                TransferSuccessful = ctx.GetValue<bool?>("transfer_successful") ?? false,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }
}
