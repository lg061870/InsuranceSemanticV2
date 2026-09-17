namespace ConversaCore.BlazorTemplateHost.Contracts;

/// <summary>
/// Immutable, versioned notification payload emitted across the host boundary to notify
/// the containing host application of topic domain events without exposing workflow context.
/// </summary>
public sealed record SampleHostNotification(
    string NotificationId,
    string Title,
    string Message,
    string Severity,
    DateTimeOffset Timestamp);

/// <summary>
/// Immutable, correlated interaction request emitted across the host boundary when the topic flow
/// requires an explicit decision or action performed in the containing host shell.
/// </summary>
public sealed record SampleHostInteractionRequest(
    string RequestId,
    string Prompt,
    IReadOnlyList<string> AvailableOptions,
    string CorrelationData);

/// <summary>
/// Immutable, typed response sent by the containing host shell to resume the topic flow awaiting
/// a matching <see cref="SampleHostInteractionRequest"/>.
/// </summary>
public sealed record SampleHostInteractionResponse(
    string SelectedOption,
    string? Comments,
    bool Confirmed);
