namespace InsuranceSemanticV2.Core.DTO;

public class LeadFollowUpResponse : BaseResponse<LeadFollowUpRequest> {
    public int FollowupId { get; set; }
}
