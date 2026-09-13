namespace InsuranceSemanticV2.Core.DTO;

/// <summary>
/// Minimal command used to make a completed, qualified lead available to human-agent systems.
/// </summary>
/// <param name="LeadId">The persisted lead identifier.</param>
/// <param name="QualificationScore">The optional validated qualification score, from 0 through 100.</param>
public sealed record QualifiedLeadHandoffRequest(int LeadId, int? QualificationScore);

/// <summary>Result of accepting a qualified lead into the human-agent boundary.</summary>
/// <param name="LeadId">The persisted lead identifier.</param>
/// <param name="Status">The resulting lead status.</param>
/// <param name="AcceptedAt">The UTC time at which the handoff was first accepted.</param>
/// <param name="AlreadyAccepted">Whether the lead had already crossed the handoff boundary.</param>
public sealed record QualifiedLeadHandoffResponse(
    int LeadId,
    string Status,
    DateTimeOffset AcceptedAt,
    bool AlreadyAccepted);
