// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class LeadAppointmentResponse
    : BaseResponse<LeadAppointmentRequest> {
    public int AppointmentId { get; set; }
}
