using ConversaCore.Models;
using ConversaCore.Runtime;

namespace ConversaCore.UI.Models;

/// <summary>Projects domain-neutral runtime output into reusable chat presentation state.</summary>
public sealed class ConversationOutputViewState
{
    private readonly List<ChatMessage> _messages = [];

    /// <summary>Gets the ordered transcript currently presented by the chat component.</summary>
    public List<ChatMessage> Messages => _messages;

    /// <summary>Gets whether the standard text prompt currently accepts input.</summary>
    public bool IsPromptEnabled { get; private set; } = true;

    /// <summary>Applies one immutable runtime output and reports its presentation effects.</summary>
    public ConversationOutputChange Apply(ConversationOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        switch (output)
        {
            case MessageOutput message:
                _messages.Add(new ChatMessage
                {
                    Content = message.Message,
                    IsFromUser = false,
                    Timestamp = message.OccurredAtUtc.LocalDateTime
                });
                return new(true, true, false);

            case AdaptiveCardOutput card:
                var existing = card.RenderMode == ConversationCardRenderMode.Replace
                    ? _messages.FirstOrDefault(item => item.IsAdaptiveCard && item.CardId == card.CardId)
                    : null;
                if (existing is not null)
                {
                    existing.AdaptiveCardJson = card.CardJson;
                    existing.Timestamp = card.OccurredAtUtc.LocalDateTime;
                    existing.IsActive = true;
                }
                else
                {
                    _messages.Add(new ChatMessage
                    {
                        IsFromUser = false,
                        IsAdaptiveCard = true,
                        AdaptiveCardJson = card.CardJson,
                        CardId = card.CardId,
                        IsActive = true,
                        Timestamp = card.OccurredAtUtc.LocalDateTime
                    });
                }
                return new(true, true, false);

            case CardStateOutput cardState:
                var stateChanged = false;
                foreach (var item in _messages.Where(item =>
                             item.IsAdaptiveCard && item.CardId == cardState.CardId))
                {
                    var active = cardState.State == ConversationCardState.Active;
                    stateChanged |= item.IsActive != active;
                    item.IsActive = active;
                }
                return new(stateChanged, false,
                    stateChanged && cardState.State == ConversationCardState.Active);

            case PromptStateOutput prompt:
                var promptEnabled = prompt.State == ConversationPromptState.Enabled;
                var promptChanged = IsPromptEnabled != promptEnabled;
                IsPromptEnabled = promptEnabled;
                return new(promptChanged, false, false);

            default:
                return ConversationOutputChange.None;
        }
    }

    /// <summary>Sets prompt state while running through the temporary legacy adapter.</summary>
    public void SetPromptEnabled(bool enabled) => IsPromptEnabled = enabled;

    /// <summary>Clears transcript presentation while retaining the conversation identity.</summary>
    public void Clear() => _messages.Clear();
}

/// <summary>Describes UI work caused by one projected conversation output.</summary>
public readonly record struct ConversationOutputChange(
    bool StateChanged,
    bool ClearBusyIndicator,
    bool ScrollToBottom)
{
    /// <summary>An output with no standard chat presentation effect.</summary>
    public static ConversationOutputChange None { get; } = new(false, false, false);
}
