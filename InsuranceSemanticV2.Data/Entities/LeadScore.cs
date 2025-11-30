namespace InsuranceSemanticV2.Data.Entities;

public class LeadScore {
    public int LeadScoreId { get; set; }
    public int LeadId { get; set; }

    public string ScoreType { get; set; } = string.Empty; // "Health", "Goals", etc.
    public int ScoreValue { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }  // <<< Added

    public Lead? Lead { get; set; }
}
