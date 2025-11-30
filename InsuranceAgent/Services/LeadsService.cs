using InsuranceSemanticV2.Core.DTO;
using System.Net.Http.Json;

namespace InsuranceAgent.Services;

public class LeadsService {
    private readonly HttpClient _http;
    private readonly ILogger<LeadsService> _logger;

    public LeadsService(HttpClient http, ILogger<LeadsService> logger) {
        _http = http;
        _logger = logger;
    }

    public async Task<int?> CreateLeadAsync(LeadRequest request) {
        var response = await _http.PostAsJsonAsync("/api/leads", request);
        if (!response.IsSuccessStatusCode) {
            _logger.LogWarning("Failed to create lead: {StatusCode}", response.StatusCode);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<LeadResponse>();
        return result?.LeadId;
    }

    public async Task<bool> UpdateLeadAsync(int leadId, LeadRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/leads/{leadId}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<LeadResponse?> GetLeadByIdAsync(int leadId) {
        return await _http.GetFromJsonAsync<LeadResponse>($"/api/leads/{leadId}");
    }

    public async Task<bool> SaveLifeGoalsAsync(LifeGoalsRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/goals", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveCoverageIntentAsync(CoverageIntentRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/coverage", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveHealthInfoAsync(HealthInfoRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/health", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveDependentsAsync(DependentsRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/dependents", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveEmploymentAsync(EmploymentRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/employment", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveBeneficiariesAsync(BeneficiaryInfoRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/beneficiary", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveContactInfoAsync(ContactInfoRequest request) {
        var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/contact-info", request);
        return response.IsSuccessStatusCode;
    }
}