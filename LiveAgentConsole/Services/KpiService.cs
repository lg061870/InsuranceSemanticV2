using InsuranceSemanticV2.Core.DTO;
using System.Net.Http.Json;

namespace LiveAgentConsole.Services;

public class KpiService(HttpClient http) {
    public async Task<LeadKpiResponse?> GetKpisAsync() {
        try {
            return await http.GetFromJsonAsync<LeadKpiResponse>("api/leads/kpis");
        } catch (Exception ex) {
            Console.WriteLine($"Error fetching KPIs: {ex.Message}");
            return null;
        }
    }
}
