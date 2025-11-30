namespace InsuranceSemanticV2.Data.Entities;

// ------------------ Dependents ------------------

public class Dependents {
    public int DependentsId { get; set; }
    public int ProfileId { get; set; }

    public string MaritalStatusValue { get; set; }
    public string HasDependentsValue { get; set; }
    public int? NoOfChildren { get; set; }

    public bool AgeRange0To5 { get; set; }
    public bool AgeRange6To12 { get; set; }
    public bool AgeRange13To17 { get; set; }
    public bool AgeRange18To25 { get; set; }
    public bool AgeRange25Plus { get; set; }

    public LeadProfile Profile { get; set; }
}
