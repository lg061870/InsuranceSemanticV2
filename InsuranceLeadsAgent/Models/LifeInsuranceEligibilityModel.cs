namespace InsuranceLeadsAgent.Models; 

public class LifeInsuranceEligibilityModel {
    public int Age { get; set; }
    public bool IsSmoker { get; set; }
    public double HeightInInches { get; set; }
    public double WeightInPounds { get; set; }
    public List<string> MedicalConditions { get; set; } = new();
}