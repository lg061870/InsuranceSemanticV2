using InsuranceSemanticV2.Core.DTO;
using LiveAgentConsole.ViewModels;
using System.Net.Http.Json;

namespace LiveAgentConsole.Services;

public class LeadService(HttpClient http) {
    private static readonly Random _rng = new();

    public async Task<List<LeadRowView>> GetAllLeadsAsync() {
        var response = await http.GetFromJsonAsync<LeadResponse>("api/leads");
        if (response?.Payload == null) return [];

        return response.Payload.Select(lead => new LeadRowView {
            Id = lead.LeadId,
            Name = lead.FullName ?? "Unknown",
            Email = lead.Email ?? "",
            Status = lead.Status ?? "New",
            AssignedAgentId = lead.AssignedAgentId,

            AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(lead.FullName ?? "U")}&background=random",

            // Map real data from API
            Product = lead.LeadIntent ?? "Life Insurance",
            SubProduct = "Whole Life", // TODO: Add to Lead entity if needed
            ProductIcon = "fa-solid fa-shield-heart",

            Source = lead.LeadSource ?? "Unknown",
            Campaign = "Fall Campaign", // TODO: Add to Lead entity if needed
            NextStep = lead.FollowUpRequired == true ? "Follow-up required" : "Review",
            Value = "$500", // TODO: Calculate from coverage amount if available

            Score = lead.QualificationScore ?? _rng.Next(40, 90),

            // Progress tracking
            HasContactInfo = lead.HasContactInfo,
            HasHealthInfo = lead.HasHealthInfo,
            HasLifeGoals = lead.HasLifeGoals,
            HasCoverageIntent = lead.HasCoverageIntent,
            HasDependents = lead.HasDependents,
            HasEmployment = lead.HasEmployment,
            HasBeneficiaryInfo = lead.HasBeneficiaryInfo,
            HasAssetsLiabilities = lead.HasAssetsLiabilities
        }).ToList();

    }
}
