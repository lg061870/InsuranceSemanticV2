using AutoMapper;
using ConversaCore.Tools;
using InsuranceAgent.Models;
using InsuranceAgent.Services;
using InsuranceAgent.Topics;
using InsuranceSemanticV2.Core.DTO;

namespace InsuranceAgent.Tools;

/// <summary>Input for creating the lead after the lead-details card is completed.</summary>
public sealed record CreateLeadRequest(LeadDetailsModel LeadDetails, ContactInfoModel? ContactInfo);

/// <summary>Typed result returned by the lead-creation capability.</summary>
public sealed record CreateLeadResult(int LeadId);

/// <summary>Input for persisting the life-goals profile section.</summary>
public sealed record SaveLifeGoalsRequest(int LeadId, LifeGoalsModel Model);

/// <summary>Result shared by successful profile persistence tools.</summary>
public sealed record ProfileWriteResult(int LeadId, string Section);

/// <summary>Persists a lead without depending on a page event subscriber.</summary>
public sealed class CreateLeadTool : IConversaTool<CreateLeadRequest, CreateLeadResult>
{
    private readonly LeadsService _leads;
    private readonly IMapper _mapper;

    public CreateLeadTool(LeadsService leads, IMapper mapper)
    {
        _leads = leads;
        _mapper = mapper;
    }

    public ToolDescriptor Descriptor => new(
        "insurance.lead.create", "1", "Create insurance lead",
        "Creates a lead from collected insurance qualification details.",
        typeof(CreateLeadRequest), typeof(CreateLeadResult),
        sideEffect: ToolSideEffect.Mutating,
        implementationType: typeof(CreateLeadTool));

    public async ValueTask<ToolResult<CreateLeadResult>> ExecuteAsync(
        CreateLeadRequest request, ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lead = _mapper.Map<LeadRequest>(request.LeadDetails);
        if (request.ContactInfo is not null)
        {
            lead.FullName = request.ContactInfo.FullName;
            lead.Email = request.ContactInfo.EmailAddress;
            lead.Phone = request.ContactInfo.PhoneNumber;
        }

        var leadId = await _leads.CreateLeadAsync(lead).ConfigureAwait(false);
        return leadId is int id
            ? ToolResult<CreateLeadResult>.Success(new CreateLeadResult(id))
            : ToolResult<CreateLeadResult>.Failure("lead.create_failed", "The lead could not be created.");
    }
}

/// <summary>Persists life-goals data independently of the containing page.</summary>
public sealed class SaveLifeGoalsTool : IConversaTool<SaveLifeGoalsRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads;
    private readonly IMapper _mapper;

    public SaveLifeGoalsTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }

    public ToolDescriptor Descriptor => new(
        "insurance.profile.life-goals.save", "1", "Save life goals",
        "Persists the collected life-goals profile section.",
        typeof(SaveLifeGoalsRequest), typeof(ProfileWriteResult),
        sideEffect: ToolSideEffect.Mutating, implementationType: typeof(SaveLifeGoalsTool));

    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(
        SaveLifeGoalsRequest request, ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dto = _mapper.Map<InsuranceSemanticV2.Core.DTO.LifeGoalsRequest>(request.Model);
        dto.LeadId = request.LeadId;
        if (!await _leads.SaveLifeGoalsAsync(dto).ConfigureAwait(false))
            return ToolResult<ProfileWriteResult>.Failure("profile.life_goals_failed", "Life-goals data could not be saved.");
        return ToolResult<ProfileWriteResult>.Success(new ProfileWriteResult(request.LeadId, "life-goals"));
    }
}
