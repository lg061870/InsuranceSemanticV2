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

        CreateMap<LifeGoalsModel, LifeGoalsRequest>();

        CreateMap<ContactInfoModel, ContactInfoRequest>()
            .ForMember(dest => dest.ContactTimeMorning, opt => opt.MapFrom(src => src.ContactTimeMorning.ToLower() == "true"))
            .ForMember(dest => dest.ContactTimeAfternoon, opt => opt.MapFrom(src => src.ContactTimeAfternoon.ToLower() == "true"))
            .ForMember(dest => dest.ContactTimeEvening, opt => opt.MapFrom(src => src.ContactTimeEvening.ToLower() == "true"))
            .ForMember(dest => dest.ContactTimeAny, opt => opt.MapFrom(src => src.ContactTimeAny.ToLower() == "true"))
            .ForMember(dest => dest.ContactMethodPhone, opt => opt.MapFrom(src => src.ContactMethodPhone.ToLower() == "true"))
            .ForMember(dest => dest.ContactMethodEmail, opt => opt.MapFrom(src => src.ContactMethodEmail.ToLower() == "true"))
            .ForMember(dest => dest.ContactMethodEither, opt => opt.MapFrom(src => src.ContactMethodEither.ToLower() == "true"))
            .ForMember(dest => dest.ConsentContact, opt => opt.MapFrom(src => src.ConsentContact.ToLower() == "yes"));

        CreateMap<CoverageIntentModel, CoverageIntentRequest>();

        CreateMap<HealthInfoModel, HealthInfoRequest>();

        CreateMap<DependentsModel, DependentsRequest>()
            .ForMember(dest => dest.NoOfChildren, opt => opt.MapFrom(src =>
                src.ChildrenCount > 0 ? (int?)src.ChildrenCount : null))
            .ForMember(dest => dest.AgeRange0To5, opt => opt.MapFrom(src => src.AgeRange0To5 == "true"))
            .ForMember(dest => dest.AgeRange6To12, opt => opt.MapFrom(src => src.AgeRange6To12 == "true"))
            .ForMember(dest => dest.AgeRange13To17, opt => opt.MapFrom(src => src.AgeRange13To17 == "true"))
            .ForMember(dest => dest.AgeRange18To25, opt => opt.MapFrom(src => src.AgeRange18To25 == "true"))
            .ForMember(dest => dest.AgeRange25Plus, opt => opt.MapFrom(src => src.AgeRange25Plus == "true"));

        CreateMap<EmploymentModel, EmploymentRequest>();

        CreateMap<BeneficiaryInfoModel, BeneficiaryInfoRequest>();

    }
}
