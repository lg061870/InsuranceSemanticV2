using ConversaCore.Context;
using ConversaCore.Registration;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Runtime;

/// <summary>
/// Default implementation of <see cref="IConversationSession"/>. Composes an injected
/// <see cref="IConversationContext"/> for conversation identity, shared values, topic
/// history, and the subtopic call stack, and adds the two pieces of state that are
/// genuinely new for CC-201: a typed <see cref="ActiveTopic"/> and a pending
/// host-interaction registry. See <see cref="IConversationSession"/> remarks for the full
/// design rationale.
/// </summary>
/// <remarks>
/// This type is scoped: target architecture section 13's DI lifetimes table lists
/// "Conversation session/context" as Scoped ("Mutable per-conversation state"). Wiring
/// this type (and the <see cref="IConversationContext"/> it composes) into the service
/// collection is explicitly out of scope for this ticket — see
/// <see cref="IConversationSession"/> remarks and the CC-201 task text ("Not DI
/// registration wiring into <c>AddConversaCore</c>/<c>ConversaCoreBuilder</c>").
/// </remarks>
public sealed class ConversationSession : IConversationSession
{
    private readonly IConversationContext _context;
    private readonly ILogger<ConversationSession>? _logger;
    private readonly HashSet<string> _pendingHostInteractionIds = new();

    /// <inheritdoc />
    public string ConversationId => _context.ConversationId;

    /// <inheritdoc />
    public string Subject => _context.UserId;

    /// <inheritdoc />
    public TopicDescriptor? ActiveTopic { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> TopicHistory => _context.TopicHistory;

    /// <inheritdoc />
    public int TopicCallDepth => _context.GetTopicCallDepth();

    /// <inheritdoc />
    public IReadOnlyCollection<string> PendingHostInteractionIds => _pendingHostInteractionIds.ToArray();

    /// <summary>
    /// Creates a new conversation session over an existing conversation context.
    /// </summary>
    /// <param name="context">
    /// The conversation context to compose for identity, shared values, topic history,
    /// and the subtopic call stack. Must not be null.
    /// </param>
    /// <param name="logger">Optional logger for structured diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public ConversationSession(IConversationContext context, ILogger<ConversationSession>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    /// <inheritdoc />
    public void SetActiveTopic(TopicDescriptor? topic)
    {
        ActiveTopic = topic;

        if (topic is not null)
        {
            _context.AddTopicToHistory(topic.TopicId);
        }

        _logger?.LogDebug(
            "[ConversationSession] Active topic for conversation {ConversationId} set to {TopicId}",
            ConversationId,
            topic?.TopicId ?? "(none)");
    }

    /// <inheritdoc />
    public void PushTopicCall(string callingTopicId, string subTopicId, object? resumeData = null)
        => _context.PushTopicCall(callingTopicId, subTopicId, resumeData);

    /// <inheritdoc />
    public TopicCallInfo? PopTopicCall(object? completionData = null)
        => _context.PopTopicCall(completionData);

    /// <inheritdoc />
    public bool IsTopicInCallStack(string topicId)
        => _context.IsTopicInCallStack(topicId);

    /// <inheritdoc />
    public void SetValue(string key, object value)
        => _context.SetValue(key, value);

    /// <inheritdoc />
    public T GetValue<T>(string key, T defaultValue = default!)
        => _context.GetValue(key, defaultValue);

    /// <inheritdoc />
    public bool TryGetValue<T>(string key, out T value)
        => _context.TryGetValue(key, out value);

    /// <inheritdoc />
    public bool HasValue(string key)
        => _context.HasValue(key);

    /// <inheritdoc />
    public void RegisterPendingHostInteraction(string requestId)
    {
        ValidateRequestId(requestId);

        if (!_pendingHostInteractionIds.Add(requestId))
        {
            throw new InvalidOperationException(
                $"Host interaction '{requestId}' is already registered as pending for conversation '{ConversationId}'.");
        }

        _logger?.LogDebug(
            "[ConversationSession] Registered pending host interaction {RequestId} for conversation {ConversationId}",
            requestId,
            ConversationId);
    }

    /// <inheritdoc />
    public bool IsHostInteractionPending(string requestId)
    {
        ValidateRequestId(requestId);
        return _pendingHostInteractionIds.Contains(requestId);
    }

    /// <inheritdoc />
    public bool TryResolvePendingHostInteraction(string requestId)
    {
        ValidateRequestId(requestId);

        var resolved = _pendingHostInteractionIds.Remove(requestId);

        if (resolved)
        {
            _logger?.LogDebug(
                "[ConversationSession] Resolved pending host interaction {RequestId} for conversation {ConversationId}",
                requestId,
                ConversationId);
        }
        else
        {
            _logger?.LogWarning(
                "[ConversationSession] Rejected response for host interaction {RequestId} on conversation {ConversationId}: not currently pending (unknown, late, or duplicate)",
                requestId,
                ConversationId);
        }

        return resolved;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _context.Reset();
        ActiveTopic = null;
        _pendingHostInteractionIds.Clear();

        _logger?.LogInformation(
            "[ConversationSession] Session reset for conversation {ConversationId}",
            ConversationId);
    }

    private static void ValidateRequestId(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Request ID must not be null, empty, or whitespace.", nameof(requestId));
        }
    }
}
