using AutoMapper;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.Entities;
using InsuranceAgent.Topics;

namespace InsuranceAgent.Mappings;

public class MappingProfile : Profile {
    public MappingProfile() {
        // Entity <-> DTO
        CreateMap<Lead, LeadRequest>().ReverseMap();
        CreateMap<Lead, LeadResponse>().ReverseMap();

        // AdaptiveCard Model -> DTO
        CreateMap<LeadDetailsModel, LeadRequest>()
            .ForMember(dest => dest.LeadSource, opt => opt.MapFrom(src => src.NormalizedLeadSource))
.ForMember(dest => dest.Language, opt => opt.MapFrom(src => src.NormalizedLanguage))
.ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.SalesPriorityLevel)) // for lead triage
.ForMember(dest => dest.LeadIntent, opt => opt.MapFrom(src => src.NormalizedLeadIntent))
.ForMember(dest => dest.InterestLevel, opt => opt.MapFrom(src => src.NormalizedInterestLevel))
.ForMember(dest => dest.QualificationScore, opt => opt.MapFrom(src => src.LeadQualificationScore))
.ForMember(dest => dest.FollowUpRequired, opt => opt.MapFrom(src => src.FollowUpNeeded))
.ForMember(dest => dest.AppointmentDateTime, opt => opt.MapFrom(src => src.ParsedAppointmentDateTime))
.ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.NotesForSalesAgent));

    }
}
