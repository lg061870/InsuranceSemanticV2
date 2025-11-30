// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   LEAD APPOINTMENT
// ============================================================

public class LeadAppointmentRequest {
    public DateTime ScheduledFor { get; set; }
    public string Method { get; set; }
    public string Notes { get; set; }
}
