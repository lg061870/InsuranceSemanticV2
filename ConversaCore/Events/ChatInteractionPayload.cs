using System;

namespace ConversaCore.Events;

/// <summary>
/// Describes a high-level interaction request from a TopicFlow activity
/// to the chat window (e.g., highlight the prompt, show a hint message).
/// Routed through CustomEventTriggered events.
/// </summary>
public sealed class ChatInteractionPayload
{
    public ChatInteractionType Type { get; set; } = ChatInteractionType.RequireUserAttention;

    /// <summary>
    /// Optional human-readable message to surface near the prompt
    /// (e.g., "Please answer the question above").
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Optional suggested duration (in milliseconds) for any transient
    /// visual effect like a pulse or highlight.
    /// </summary>
    public int? DurationMs { get; set; }
}

/// <summary>
/// Well-known chat interaction kinds that activities can request.
/// Extend this as new UX patterns emerge.
/// </summary>
public enum ChatInteractionType
{
    /// <summary>
    /// Ask the chat window to briefly highlight or otherwise draw
    /// attention to the user input prompt.
    /// </summary>
    RequireUserAttention = 0,
}
