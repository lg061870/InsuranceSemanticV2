namespace InsuranceSemanticV2.Core.DTO;

public class ContactAttemptResponse : BaseResponse<ContactAttemptRequest> {
    public int AttemptId { get; set; }
}