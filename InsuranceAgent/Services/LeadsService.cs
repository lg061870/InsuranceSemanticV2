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
        try {
            _logger.LogInformation("[LeadsService] Creating lead: Email={Email}, Phone={Phone}",
                request.Email, request.Phone);

            var response = await _http.PostAsJsonAsync("/api/leads", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to create lead. Status={StatusCode}, Body={ErrorBody}",
                    response.StatusCode, errorBody);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<LeadResponse>();
            _logger.LogInformation("[LeadsService] Lead created successfully. LeadId={LeadId}", result?.LeadId);
            return result?.LeadId;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception creating lead: Email={Email}", request.Email);
            return null;
        }
    }

    public async Task<bool> UpdateLeadAsync(int leadId, LeadRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Updating lead: LeadId={LeadId}", leadId);

            var response = await _http.PutAsJsonAsync($"/api/leads/{leadId}", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to update lead. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    leadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Lead updated successfully. LeadId={LeadId}", leadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception updating lead: LeadId={LeadId}", leadId);
            return false;
        }
    }

    public async Task<LeadResponse?> GetLeadByIdAsync(int leadId) {
        try {
            _logger.LogInformation("[LeadsService] Getting lead: LeadId={LeadId}", leadId);

            var result = await _http.GetFromJsonAsync<LeadResponse>($"/api/leads/{leadId}");

            if (result == null) {
                _logger.LogWarning("[LeadsService] Lead not found: LeadId={LeadId}", leadId);
            }

            return result;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception getting lead: LeadId={LeadId}", leadId);
            return null;
        }
    }

    public async Task<bool> SaveLifeGoalsAsync(LifeGoalsRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving life goals: LeadId={LeadId}", request.LeadId);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/goals", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save life goals. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Life goals saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving life goals: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }

    public async Task<bool> SaveCoverageIntentAsync(CoverageIntentRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving coverage intent: LeadId={LeadId}", request.LeadId);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/coverage", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save coverage intent. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Coverage intent saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving coverage intent: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }

    public async Task<bool> SaveHealthInfoAsync(HealthInfoRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving health info: LeadId={LeadId}", request.LeadId);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/health", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save health info. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Health info saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving health info: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }

    public async Task<bool> SaveDependentsAsync(DependentsRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving dependents: LeadId={LeadId}", request.LeadId);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/dependents", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save dependents. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Dependents saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving dependents: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }

    public async Task<bool> SaveEmploymentAsync(EmploymentRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving employment: LeadId={LeadId}", request.LeadId);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/employment", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save employment. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Employment saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving employment: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }

    public async Task<bool> SaveBeneficiariesAsync(BeneficiaryInfoRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving beneficiaries: LeadId={LeadId}", request.LeadId);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/beneficiary", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save beneficiaries. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Beneficiaries saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving beneficiaries: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }

    public async Task<bool> SaveContactInfoAsync(ContactInfoRequest request) {
        try {
            _logger.LogInformation("[LeadsService] Saving contact info: LeadId={LeadId}, Email={Email}",
                request.LeadId, request.EmailAddress);

            var response = await _http.PutAsJsonAsync($"/api/profile/{request.LeadId}/contact-info", request);

            if (!response.IsSuccessStatusCode) {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[LeadsService] Failed to save contact info. LeadId={LeadId}, Status={StatusCode}, Body={ErrorBody}",
                    request.LeadId, response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("[LeadsService] Contact info saved successfully. LeadId={LeadId}", request.LeadId);
            return true;

        } catch (Exception ex) {
            _logger.LogError(ex, "[LeadsService] Exception saving contact info: LeadId={LeadId}", request.LeadId);
            return false;
        }
    }
}
