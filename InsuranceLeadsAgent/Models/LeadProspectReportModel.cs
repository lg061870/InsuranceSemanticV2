namespace InsuranceLeadsAgent.Models
{
    public class LeadProspectReportModel
    {
        public string FullName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string QualificationTier { get; set; } = string.Empty;
        public string UnderwritingClassification { get; set; } = string.Empty;
        public string ContactMethod { get; set; } = string.Empty;
        public bool IntakeCompleted { get; set; }
        public bool TransferAttempted { get; set; }
        public bool TransferSuccessful { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
