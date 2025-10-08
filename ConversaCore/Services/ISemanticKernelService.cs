using System.Threading.Tasks;
using ConversaCore.Events;
using ConversaCore.Models;

namespace ConversaCore.Services;
/// <summary>
/// Defines a contract for processing user messages using Semantic Kernel.
/// Strongly typed with ChatSessionState and SemanticKernelResponse.
/// </summary>

public interface ISemanticKernelService {
    // Events
    event EventHandler<SemanticMessageEventArgs>? SemanticMessageReady;
    event EventHandler<SemanticAdaptiveCardEventArgs>? SemanticAdaptiveCardReady;
    event EventHandler<SemanticChatEventArgs>? SemanticChatEventRaised;
    event EventHandler<SemanticTypingEventArgs>? SemanticTypingIndicatorChanged;

    // Invocation
    Task<SemanticKernelResponse> ProcessMessageAsync(string userMessage, ChatSessionStateBase sessionState);
}


