namespace ConversaCore.Models; 
/// <summary>
/// Concrete response type from Semantic Kernel, extending the base response.
/// Includes events triggered by LLM output.
/// </summary>
public class SemanticKernelResponse : SemanticKernelResponseBase {
    /// <summary>
    /// Optional list of events extracted from the LLM response.
    /// </summary>
    public List<ChatEvent> Events { get; set; } = new();
}
