using InsuranceSemanticV2.Data.Entities;

namespace InsuranceSemanticV2.Core.Entities;

// ------------------ LifeGoals ------------------

public class LifeGoals {
    public int LifeGoalsId { get; set; }
    public int ProfileId { get; set; }

    public string ProtectLovedOnesString { get; set; }
    public string PayMortgageString { get; set; }
    public string PrepareFutureString { get; set; }
    public string PeaceOfMindString { get; set; }
    public string CoverExpensesString { get; set; }
    public string UnsureString { get; set; }

    public LeadProfile Profile { get; set; }
}
