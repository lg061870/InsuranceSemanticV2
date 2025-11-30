namespace InsuranceSemanticV2.Api.Mapping;

using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.Entities;

public static class DtoToEntityExtensions {

    public static Lead ToEntity(this LeadRequest dto) => new() {
        FullName = dto.FullName,
        Email = dto.Email,
        Phone = dto.Phone,
        Status = dto.Status,
        AssignedAgentId = dto.AssignedAgentId,
        CreatedAt = dto.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,

        LeadSource = dto.LeadSource,
        Language = dto.Language,
        LeadIntent = dto.LeadIntent,
        InterestLevel = dto.InterestLevel,

        QualificationScore = dto.QualificationScore,
        FollowUpRequired = dto.FollowUpRequired,
        AppointmentDateTime = dto.AppointmentDateTime,

        LeadUrl = dto.LeadUrl,
        Notes = dto.Notes
    };

    public static ContactInfo ToEntity(this ContactInfoRequest dto, int profileId) => new() {
        ProfileId = profileId,
        FullName = dto.FullName,
        DateOfBirth = dto.DateOfBirth,
        PhoneNumber = dto.PhoneNumber,
        EmailAddress = dto.EmailAddress,
        StreetAddress = dto.StreetAddress,
        City = dto.City,
        State = dto.State,
        ZipCode = dto.ZipCode,
        ContactTimeMorning = dto.ContactTimeMorning,
        ContactTimeAfternoon = dto.ContactTimeAfternoon,
        ContactTimeEvening = dto.ContactTimeEvening,
        ContactTimeAny = dto.ContactTimeAny,
        ContactMethodPhone = dto.ContactMethodPhone,
        ContactMethodEmail = dto.ContactMethodEmail,
        ContactMethodEither = dto.ContactMethodEither,
        ConsentContact = dto.ConsentContact
    };

    public static Compliance ToEntity(this ComplianceRequest dto, int profileId) => new() {
        ProfileId = profileId,
        TcpaConsent = dto.TcpaConsent,
        ZipCode = dto.ZipCode
    };

    public static CaliforniaResident ToEntity(this CaliforniaResidentRequest dto, int profileId) => new() {
        ProfileId = profileId,
        ZipCode = dto.ZipCode,
        CcpaAcknowledged = dto.CcpaAcknowledged
    };

    public static Employment ToEntity(this EmploymentRequest dto, int profileId) => new() {
        ProfileId = profileId,
        EmploymentStatusValue = dto.EmploymentStatusValue,
        HouseholdIncomeValue = dto.HouseholdIncomeValue,
        Occupation = dto.Occupation,
        YearsEmployedValue = dto.YearsEmployedValue
    };

    public static CoverageIntent ToEntity(this CoverageIntentRequest dto, int profileId) => new() {
        ProfileId = profileId,
        CoverageType = dto.CoverageType,
        CoverageStartTime = dto.CoverageStartTime,
        CoverageAmount = dto.CoverageAmount,
        MonthlyBudget = dto.MonthlyBudget
    };

    public static HealthInfo ToEntity(this HealthInfoRequest dto, int profileId) => new() {
        ProfileId = profileId,
        TobaccoUse = dto.TobaccoUse,
        ConditionDiabetes = dto.ConditionDiabetes,
        ConditionHeart = dto.ConditionHeart,
        ConditionBloodPressure = dto.ConditionBloodPressure,
        ConditionNone = dto.ConditionNone,
        HealthInsurance = dto.HealthInsurance,
        Height = dto.Height,
        Weight = dto.Weight,
        OverallHealthStatus = dto.OverallHealthStatus,
        CurrentMedications = dto.CurrentMedications,
        FamilyMedicalHistory = dto.FamilyMedicalHistory
    };

    public static Dependents ToEntity(this DependentsRequest dto, int profileId) => new() {
        ProfileId = profileId,
        MaritalStatusValue = dto.MaritalStatusValue,
        HasDependentsValue = dto.HasDependentsValue,
        NoOfChildren = dto.NoOfChildren,
        AgeRange0To5 = dto.AgeRange0To5,
        AgeRange6To12 = dto.AgeRange6To12,
        AgeRange13To17 = dto.AgeRange13To17,
        AgeRange18To25 = dto.AgeRange18To25,
        AgeRange25Plus = dto.AgeRange25Plus
    };

    public static AssetsLiabilities ToEntity(this AssetsLiabilitiesRequest dto, int profileId) => new() {
        ProfileId = profileId,
        HasHomeEquityValue = dto.HasHomeEquityValue,
        HomeEquityAmount = dto.HomeEquityAmount,
        SavingsAmount = dto.SavingsAmount,
        InvestmentsAmount = dto.InvestmentsAmount,
        RetirementAmount = dto.RetirementAmount,
        CreditCardDebt = dto.CreditCardDebt,
        StudentLoans = dto.StudentLoans,
        AutoLoans = dto.AutoLoans,
        MortgageDebt = dto.MortgageDebt,
        OtherDebt = dto.OtherDebt
    };
}