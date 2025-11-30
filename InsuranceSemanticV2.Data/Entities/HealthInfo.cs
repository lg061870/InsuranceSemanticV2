namespace InsuranceSemanticV2.Data.Entities;

// ------------------ HealthInfo ------------------

public class HealthInfo {
    public int HealthInfoId { get; set; }
    public int ProfileId { get; set; }

    public string TobaccoUse { get; set; }
    public string ConditionDiabetes { get; set; }
    public string ConditionHeart { get; set; }
    public string ConditionBloodPressure { get; set; }
    public string ConditionNone { get; set; }
    public string HealthInsurance { get; set; }

    public string Height { get; set; }
    public string Weight { get; set; }

    public string OverallHealthStatus { get; set; }
    public string CurrentMedications { get; set; }
    public string FamilyMedicalHistory { get; set; }

    public LeadProfile Profile { get; set; }
}
