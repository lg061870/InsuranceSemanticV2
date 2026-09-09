namespace ConversaCore.Runtime;

/// <summary>Immutable, domain-neutral output emitted by one conversation runtime.</summary>
/// <remarks>Outputs contain identifiers and snapshots only. They never expose a mutable topic,
/// activity, service, or workflow context. CC-301 assigns delivery order per subscription.</remarks>
public abstract record ConversationOutput
{
    /// <summary>Initializes common output metadata.</summary>
    protected ConversationOutput(string conversationId, DateTimeOffset? occurredAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ConversationId = conversationId;
        OccurredAtUtc = occurredAtUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the conversation that produced this output.</summary>
    public string ConversationId { get; }

    /// <summary>Gets when the framework created this immutable output snapshot.</summary>
    public DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>Identifies the source or intended presentation role of a conversation message.</summary>
public enum ConversationMessageRole
{
    /// <summary>A normal assistant response.</summary>
    Assistant,
    /// <summary>A framework or operational notice.</summary>
    System,
    /// <summary>A message produced from a tool result.</summary>
    Tool
}

/// <summary>A plain-text message for the conversation transcript.</summary>
public sealed record MessageOutput : ConversationOutput
{
    /// <summary>Creates a message output.</summary>
    public MessageOutput(string conversationId, string message,
        ConversationMessageRole role = ConversationMessageRole.Assistant,
        DateTimeOffset? occurredAtUtc = null) : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Role = role;
    }

    /// <summary>Gets the message text.</summary>
    public string Message { get; }
    /// <summary>Gets the message's presentation role.</summary>
    public ConversationMessageRole Role { get; }
}

/// <summary>Describes how an adaptive card should be added to the transcript.</summary>
public enum ConversationCardRenderMode
{
    /// <summary>Add the card as a new transcript item.</summary>
    Append,
    /// <summary>Replace the existing card with the same identifier.</summary>
    Replace
}

/// <summary>An adaptive-card JSON snapshot for framework UI rendering.</summary>
public sealed record AdaptiveCardOutput : ConversationOutput
{
    /// <summary>Creates an adaptive-card output.</summary>
    public AdaptiveCardOutput(string conversationId, string cardId, string cardJson,
        ConversationCardRenderMode renderMode = ConversationCardRenderMode.Append,
        bool isInputRequired = false, DateTimeOffset? occurredAtUtc = null)
        : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardJson);
        CardId = cardId;
        CardJson = cardJson;
        RenderMode = renderMode;
        IsInputRequired = isInputRequired;
    }

    /// <summary>Gets the stable card identifier.</summary>
    public string CardId { get; }
    /// <summary>Gets the serialized adaptive-card snapshot.</summary>
    public string CardJson { get; }
    /// <summary>Gets how the UI should place this card.</summary>
    public ConversationCardRenderMode RenderMode { get; }
    /// <summary>Gets whether the card expects a user submission.</summary>
    public bool IsInputRequired { get; }
}

/// <summary>Presentation state of a previously emitted adaptive card.</summary>
public enum ConversationCardState
{
    /// <summary>The card accepts interaction.</summary>
    Active,
    /// <summary>The card remains visible but no longer accepts interaction.</summary>
    ReadOnly,
    /// <summary>The card is not displayed.</summary>
    Hidden
}

/// <summary>A state transition for a previously emitted adaptive card.</summary>
public sealed record CardStateOutput : ConversationOutput
{
    /// <summary>Creates a card-state output.</summary>
    public CardStateOutput(string conversationId, string cardId, ConversationCardState state,
        DateTimeOffset? occurredAtUtc = null) : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        CardId = cardId;
        State = state;
    }

    /// <summary>Gets the affected card identifier.</summary>
    public string CardId { get; }
    /// <summary>Gets the card's new state.</summary>
    public ConversationCardState State { get; }
}

/// <summary>Whether the standard chat prompt accepts input.</summary>
public enum ConversationPromptState
{
    /// <summary>The standard text prompt accepts input.</summary>
    Enabled,
    /// <summary>The standard text prompt does not accept input.</summary>
    Disabled
}

/// <summary>A state transition for the standard chat prompt.</summary>
public sealed record PromptStateOutput : ConversationOutput
{
    /// <summary>Creates a prompt-state output.</summary>
    public PromptStateOutput(string conversationId, ConversationPromptState state,
        string? relatedCardId = null, DateTimeOffset? occurredAtUtc = null)
        : base(conversationId, occurredAtUtc)
    {
        if (relatedCardId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(relatedCardId);
        State = state;
        RelatedCardId = relatedCardId;
    }

    /// <summary>Gets the prompt's new state.</summary>
    public ConversationPromptState State { get; }
    /// <summary>Gets the card whose input requirement caused the transition, when applicable.</summary>
    public string? RelatedCardId { get; }
}

/// <summary>Stable public states for a topic activation.</summary>
public enum ConversationTopicState
{
    /// <summary>The activation exists but has not started.</summary>
    Created,
    /// <summary>The activation is starting.</summary>
    Starting,
    /// <summary>The activation is executing.</summary>
    Running,
    /// <summary>The activation awaits user input.</summary>
    WaitingForInput,
    /// <summary>The activation awaits a child topic.</summary>
    WaitingForSubtopic,
    /// <summary>The activation is resuming after a wait.</summary>
    Resuming,
    /// <summary>The activation completed successfully.</summary>
    Completed,
    /// <summary>The activation failed.</summary>
    Failed,
    /// <summary>The activation was cancelled.</summary>
    Cancelled
}

/// <summary>An immutable topic-lifecycle snapshot.</summary>
public sealed record TopicLifecycleOutput : ConversationOutput
{
    /// <summary>Creates a topic-lifecycle output.</summary>
    public TopicLifecycleOutput(string conversationId, string topicId, ConversationTopicState state,
        string? detail = null, DateTimeOffset? occurredAtUtc = null) : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
        TopicId = topicId;
        State = state;
        Detail = detail;
    }

    /// <summary>Gets the registered topic identifier.</summary>
    public string TopicId { get; }
    /// <summary>Gets the topic's new state.</summary>
    public ConversationTopicState State { get; }
    /// <summary>Gets optional non-sensitive diagnostic detail.</summary>
    public string? Detail { get; }
}

/// <summary>Stable public states for an activity execution.</summary>
public enum ConversationActivityState
{
    /// <summary>The activity is inactive.</summary>
    Idle,
    /// <summary>The activity exists but has not started.</summary>
    Created,
    /// <summary>The activity is executing.</summary>
    Running,
    /// <summary>The activity rendered its visual output.</summary>
    Rendered,
    /// <summary>The activity awaits user input.</summary>
    WaitingForInput,
    /// <summary>The activity awaits a nested activity.</summary>
    WaitingForSubactivity,
    /// <summary>The activity collected user input.</summary>
    InputCollected,
    /// <summary>The activity failed input validation.</summary>
    ValidationFailed,
    /// <summary>The activity emitted its trigger.</summary>
    Triggered,
    /// <summary>The activity completed successfully.</summary>
    Completed,
    /// <summary>The activity failed.</summary>
    Failed,
    /// <summary>The activity was cancelled.</summary>
    Cancelled
}

/// <summary>An immutable activity-lifecycle snapshot.</summary>
public sealed record ActivityLifecycleOutput : ConversationOutput
{
    /// <summary>Creates an activity-lifecycle output.</summary>
    public ActivityLifecycleOutput(string conversationId, string topicId, string activityId,
        ConversationActivityState state, string? detail = null, DateTimeOffset? occurredAtUtc = null)
        : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        TopicId = topicId;
        ActivityId = activityId;
        State = state;
        Detail = detail;
    }

    /// <summary>Gets the owning registered topic identifier.</summary>
    public string TopicId { get; }
    /// <summary>Gets the activity identifier within the topic.</summary>
    public string ActivityId { get; }
    /// <summary>Gets the activity's new state.</summary>
    public ConversationActivityState State { get; }
    /// <summary>Gets optional non-sensitive diagnostic detail.</summary>
    public string? Detail { get; }
}

/// <summary>Base contract for the only domain-specific output hook exposed to a containing host.</summary>
public abstract record HostOutput : ConversationOutput
{
    /// <summary>Initializes common host-output metadata.</summary>
    protected HostOutput(string conversationId, DateTimeOffset? occurredAtUtc = null)
        : base(conversationId, occurredAtUtc) { }
}

/// <summary>Base output for a versioned, one-way notification to the containing host.</summary>
/// <remarks>CC-303 adds the strongly typed payload specialization.</remarks>
public abstract record HostNotificationOutput : HostOutput
{
    /// <summary>Initializes host-notification identity.</summary>
    protected HostNotificationOutput(string conversationId, string eventName, int version,
        DateTimeOffset? occurredAtUtc = null) : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        EventName = eventName;
        Version = version;
    }

    /// <summary>Gets the stable domain event name.</summary>
    public string EventName { get; }
    /// <summary>Gets the positive contract version.</summary>
    public int Version { get; }
}

/// <summary>Base output for an awaitable, correlated request to the containing host.</summary>
/// <remarks>CC-304 adds typed request/response payloads and completion behavior.</remarks>
public abstract record HostInteractionRequestOutput : HostOutput
{
    /// <summary>Initializes host-interaction identity and correlation metadata.</summary>
    protected HostInteractionRequestOutput(string conversationId, string requestId, string interactionName,
        int version, TimeSpan timeout, DateTimeOffset? occurredAtUtc = null) : base(conversationId, occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionName);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        RequestId = requestId;
        InteractionName = interactionName;
        Version = version;
        Timeout = timeout;
    }

    /// <summary>Gets the request correlation identifier.</summary>
    public string RequestId { get; }
    /// <summary>Gets the stable interaction contract name.</summary>
    public string InteractionName { get; }
    /// <summary>Gets the positive interaction contract version.</summary>
    public int Version { get; }
    /// <summary>Gets the maximum time allowed for a host response.</summary>
    public TimeSpan Timeout { get; }
}
