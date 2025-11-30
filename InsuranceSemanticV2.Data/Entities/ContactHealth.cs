namespace InsuranceSemanticV2.Data.Entities;

// ------------------ ContactHealth ------------------

public class ContactHealth {
    public int ContactHealthId { get; set; }
    public int ProfileId { get; set; }

    public string Address { get; set; }
    public string CityState { get; set; }
    public string DateOfBirth { get; set; }

    public bool HospitalizedPast5Years { get; set; }
    public bool CurrentlyTakingMedications { get; set; }
    public string Medications { get; set; }
    public string MedicalConditions { get; set; }
    public bool TobaccoUseLast12Months { get; set; }

    public LeadProfile Profile { get; set; }
}
