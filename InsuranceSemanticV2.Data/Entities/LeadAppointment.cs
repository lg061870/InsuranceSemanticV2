namespace InsuranceSemanticV2.Data.Entities;

public class LeadAppointment {
    public int AppointmentId { get; set; }
    public int LeadId { get; set; }

    public DateTime ScheduledFor { get; set; }
    public string Method { get; set; }
    public string Notes { get; set; }

    public Lead Lead { get; set; }
}
