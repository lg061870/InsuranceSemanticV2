using ConversaCore.Events;

namespace InsuranceAgent.Services;

/// <summary>
/// Optional interface if your SK implementation emits typing events.
/// </summary>
public interface ISemanticKernelTyping {
    event EventHandler<SemanticTypingIndicatorEventArgs> SemanticTypingIndicatorChanged;
}
