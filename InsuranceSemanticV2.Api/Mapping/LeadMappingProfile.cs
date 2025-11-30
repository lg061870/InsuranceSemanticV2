using AutoMapper;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Core.Entities;
using InsuranceSemanticV2.Data.Entities;

namespace InsuranceSemanticV2.Api.Mapping;

public class LeadMappingProfile : Profile {
    public LeadMappingProfile() {
        // Lead <-> LeadRequest
        CreateMap<Lead, LeadRequest>().ReverseMap()
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.LeadId, opt => opt.Ignore());

        // Lead -> LeadResponse
        CreateMap<Lead, LeadResponse>()
            .ForMember(dest => dest.LeadId, opt => opt.MapFrom(src => src.LeadId))
            .ForMember(dest => dest.Payload, opt => opt.MapFrom(src => new List<LeadRequest> {
                new() {
                    FullName = src.FullName,
                    Email = src.Email,
                    Phone = src.Phone,
                    Status = src.Status,
                    CreatedAt = src.CreatedAt,
                    UpdatedAt = src.UpdatedAt,
                    AssignedAgentId = src.AssignedAgentId
                }
            }));

        // Profile section mappings
        CreateMap<ContactInfo, ContactInfoRequest>().ReverseMap();
        CreateMap<HealthInfo, HealthInfoRequest>().ReverseMap();
        CreateMap<Dependents, DependentsRequest>().ReverseMap();
        CreateMap<Employment, EmploymentRequest>().ReverseMap();
        CreateMap<CoverageIntent, CoverageIntentRequest>().ReverseMap();
        CreateMap<LifeGoals, LifeGoalsRequest>().ReverseMap();
        CreateMap<AssetsLiabilities, AssetsLiabilitiesRequest>().ReverseMap();
        CreateMap<Compliance, ComplianceRequest>().ReverseMap();
        CreateMap<CaliforniaResident, CaliforniaResidentRequest>().ReverseMap();
        CreateMap<BeneficiaryInfo, BeneficiaryInfoRequest>().ReverseMap();
        CreateMap<ContactHealth, ContactHealthRequest>().ReverseMap();
        CreateMap<InsuranceContext, InsuranceContextRequest>().ReverseMap();
    }
}
