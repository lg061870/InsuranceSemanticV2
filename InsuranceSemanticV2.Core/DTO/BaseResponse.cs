namespace InsuranceSemanticV2.Core.DTO;

public abstract class BaseResponse<TRequest> {
    public List<TRequest> Payload { get; set; } = new();
    public List<string>? Errors { get; set; }
    public string? Message { get; set; }
    public bool Success => Errors == null || Errors.Count == 0;
}
