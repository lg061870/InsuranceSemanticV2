namespace ConversaCore.Models; 
/// <summary>
/// Minimal base type for Semantic Kernel responses.
/// </summary>
public class SemanticKernelResponseBase {
    public string Content { get; set; } = string.Empty;
    public bool IsAdaptiveCard { get; set; }
    public string? AdaptiveCardJson { get; set; }
}
