namespace InsuranceLeadsAgent.Models
{
    public class QualificationDecision
    {
        public QualificationTier HighestTier { get; set; }
        public bool BorderlineFlag { get; set; }
        public string UnderwritingClassification { get; set; } = string.Empty;
    }
}
