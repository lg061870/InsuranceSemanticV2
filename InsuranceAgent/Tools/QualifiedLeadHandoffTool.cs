using ConversaCore.Tools;
using InsuranceAgent.Services;
using InsuranceSemanticV2.Core.DTO;

namespace InsuranceAgent.Tools;

/// <summary>
/// Moves a persisted, qualified lead across the narrow API boundary consumed by human-agent apps.
/// </summary>
public sealed class QualifiedLeadHandoffTool
    : IConversaTool<QualifiedLeadHandoffRequest, QualifiedLeadHandoffResponse>
{
    private readonly LeadsService _leads;

    /// <summary>Creates the handoff tool over the InsuranceAgent API client.</summary>
    public QualifiedLeadHandoffTool(LeadsService leads)
    {
        _leads = leads ?? throw new ArgumentNullException(nameof(leads));
    }

    /// <inheritdoc />
    public ToolDescriptor Descriptor => new(
        "insurance.lead.handoff", "1", "Hand off qualified lead",
        "Marks a persisted qualified lead as available to the separate human-agent system.",
        typeof(QualifiedLeadHandoffRequest), typeof(QualifiedLeadHandoffResponse),
        sideEffect: ToolSideEffect.Mutating,
        implementationType: typeof(QualifiedLeadHandoffTool));

    /// <inheritdoc />
    public async ValueTask<ToolResult<QualifiedLeadHandoffResponse>> ExecuteAsync(
        QualifiedLeadHandoffRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.LeadId <= 0)
            return ToolResult<QualifiedLeadHandoffResponse>.Failure(
                "lead.handoff_invalid_id", "A persisted lead is required for handoff.");
        if (request.QualificationScore is < 0 or > 100)
            return ToolResult<QualifiedLeadHandoffResponse>.Failure(
                "lead.handoff_invalid_score", "Qualification score must be between 0 and 100.");

        var result = await _leads
            .HandoffQualifiedLeadAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return result is not null
            ? ToolResult<QualifiedLeadHandoffResponse>.Success(result)
            : ToolResult<QualifiedLeadHandoffResponse>.Failure(
                "lead.handoff_failed", "The qualified lead could not be handed off.");
    }
}
