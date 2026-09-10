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

/// <summary>Input for persisting contact information.</summary>
public sealed record SaveContactInfoRequest(int LeadId, ContactInfoModel Model);

/// <summary>Input for persisting coverage intent.</summary>
public sealed record SaveCoverageIntentRequest(int LeadId, CoverageIntentModel Model);

/// <summary>Input for persisting health information.</summary>
public sealed record SaveHealthInfoRequest(int LeadId, HealthInfoModel Model);

/// <summary>Input for persisting dependent information.</summary>
public sealed record SaveDependentsRequest(int LeadId, DependentsModel Model);

/// <summary>Input for persisting employment information.</summary>
public sealed record SaveEmploymentRequest(int LeadId, EmploymentModel Model);

/// <summary>Input for persisting beneficiary information.</summary>
public sealed record SaveBeneficiariesRequest(int LeadId, BeneficiaryInfoModel Model);

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

/// <summary>Persists contact information independently of the containing page.</summary>
public sealed class SaveContactInfoTool : IConversaTool<SaveContactInfoRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads; private readonly IMapper _mapper;
    public SaveContactInfoTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }
    public ToolDescriptor Descriptor => ProfileDescriptor("insurance.profile.contact.save", "Save contact information", typeof(SaveContactInfoRequest), typeof(SaveContactInfoTool));
    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(SaveContactInfoRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dto = _mapper.Map<ContactInfoRequest>(request.Model); dto.LeadId = request.LeadId;
        return await _leads.SaveContactInfoAsync(dto).ConfigureAwait(false)
            ? Success(request.LeadId, "contact") : Failure("contact");
    }
    private static ToolDescriptor ProfileDescriptor(string id, string name, Type request, Type implementation) =>
        new(id, "1", name, "Persists a collected insurance profile section.", request, typeof(ProfileWriteResult), sideEffect: ToolSideEffect.Mutating, implementationType: implementation);
    private static ToolResult<ProfileWriteResult> Success(int id, string section) => ToolResult<ProfileWriteResult>.Success(new(id, section));
    private static ToolResult<ProfileWriteResult> Failure(string section) => ToolResult<ProfileWriteResult>.Failure($"profile.{section}_failed", $"The {section} profile section could not be saved.");
}

/// <summary>Persists coverage intent independently of the containing page.</summary>
public sealed class SaveCoverageIntentTool : IConversaTool<SaveCoverageIntentRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads; private readonly IMapper _mapper;
    public SaveCoverageIntentTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }
    public ToolDescriptor Descriptor => DescriptorFor("insurance.profile.coverage.save", "Save coverage intent", typeof(SaveCoverageIntentRequest), typeof(SaveCoverageIntentTool));
    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(SaveCoverageIntentRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var dto = _mapper.Map<CoverageIntentRequest>(request.Model); dto.LeadId = request.LeadId;
        return await _leads.SaveCoverageIntentAsync(dto).ConfigureAwait(false) ? Ok(request.LeadId, "coverage") : Fail("coverage");
    }
    internal static ToolDescriptor DescriptorFor(string id, string name, Type request, Type implementation) => new(id, "1", name, "Persists a collected insurance profile section.", request, typeof(ProfileWriteResult), sideEffect: ToolSideEffect.Mutating, implementationType: implementation);
    internal static ToolResult<ProfileWriteResult> Ok(int id, string section) => ToolResult<ProfileWriteResult>.Success(new(id, section));
    internal static ToolResult<ProfileWriteResult> Fail(string section) => ToolResult<ProfileWriteResult>.Failure($"profile.{section}_failed", $"The {section} profile section could not be saved.");
}

/// <summary>Persists health information independently of the containing page.</summary>
public sealed class SaveHealthInfoTool : IConversaTool<SaveHealthInfoRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads; private readonly IMapper _mapper;
    public SaveHealthInfoTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }
    public ToolDescriptor Descriptor => SaveCoverageIntentTool.DescriptorFor("insurance.profile.health.save", "Save health information", typeof(SaveHealthInfoRequest), typeof(SaveHealthInfoTool));
    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(SaveHealthInfoRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var dto = _mapper.Map<HealthInfoRequest>(request.Model); dto.LeadId = request.LeadId;
        return await _leads.SaveHealthInfoAsync(dto).ConfigureAwait(false) ? SaveCoverageIntentTool.Ok(request.LeadId, "health") : SaveCoverageIntentTool.Fail("health");
    }
}

/// <summary>Persists dependent information independently of the containing page.</summary>
public sealed class SaveDependentsTool : IConversaTool<SaveDependentsRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads; private readonly IMapper _mapper;
    public SaveDependentsTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }
    public ToolDescriptor Descriptor => SaveCoverageIntentTool.DescriptorFor("insurance.profile.dependents.save", "Save dependents", typeof(SaveDependentsRequest), typeof(SaveDependentsTool));
    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(SaveDependentsRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var dto = _mapper.Map<DependentsRequest>(request.Model); dto.LeadId = request.LeadId;
        return await _leads.SaveDependentsAsync(dto).ConfigureAwait(false) ? SaveCoverageIntentTool.Ok(request.LeadId, "dependents") : SaveCoverageIntentTool.Fail("dependents");
    }
}

/// <summary>Persists employment information independently of the containing page.</summary>
public sealed class SaveEmploymentTool : IConversaTool<SaveEmploymentRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads; private readonly IMapper _mapper;
    public SaveEmploymentTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }
    public ToolDescriptor Descriptor => SaveCoverageIntentTool.DescriptorFor("insurance.profile.employment.save", "Save employment", typeof(SaveEmploymentRequest), typeof(SaveEmploymentTool));
    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(SaveEmploymentRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var dto = _mapper.Map<EmploymentRequest>(request.Model); dto.LeadId = request.LeadId;
        return await _leads.SaveEmploymentAsync(dto).ConfigureAwait(false) ? SaveCoverageIntentTool.Ok(request.LeadId, "employment") : SaveCoverageIntentTool.Fail("employment");
    }
}

/// <summary>Persists beneficiary information independently of the containing page.</summary>
public sealed class SaveBeneficiariesTool : IConversaTool<SaveBeneficiariesRequest, ProfileWriteResult>
{
    private readonly LeadsService _leads; private readonly IMapper _mapper;
    public SaveBeneficiariesTool(LeadsService leads, IMapper mapper) { _leads = leads; _mapper = mapper; }
    public ToolDescriptor Descriptor => SaveCoverageIntentTool.DescriptorFor("insurance.profile.beneficiaries.save", "Save beneficiaries", typeof(SaveBeneficiariesRequest), typeof(SaveBeneficiariesTool));
    public async ValueTask<ToolResult<ProfileWriteResult>> ExecuteAsync(SaveBeneficiariesRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var dto = _mapper.Map<BeneficiaryInfoRequest>(request.Model); dto.LeadId = request.LeadId;
        return await _leads.SaveBeneficiariesAsync(dto).ConfigureAwait(false) ? SaveCoverageIntentTool.Ok(request.LeadId, "beneficiaries") : SaveCoverageIntentTool.Fail("beneficiaries");
    }
}
