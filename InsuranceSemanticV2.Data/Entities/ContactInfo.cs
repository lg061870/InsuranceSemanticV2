namespace InsuranceSemanticV2.Data.Entities;

// ------------------ ContactInfo ------------------

public class ContactInfo {
    public int ContactInfoId { get; set; }
    public int ProfileId { get; set; }

    public string FullName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string PhoneNumber { get; set; }
    public string EmailAddress { get; set; }
    public string StreetAddress { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }

    public bool ContactTimeMorning { get; set; }
    public bool ContactTimeAfternoon { get; set; }
    public bool ContactTimeEvening { get; set; }
    public bool ContactTimeAny { get; set; }

    public bool ContactMethodPhone { get; set; }
    public bool ContactMethodEmail { get; set; }
    public bool ContactMethodEither { get; set; }

    public bool ConsentContact { get; set; }

    public LeadProfile Profile { get; set; }
}
