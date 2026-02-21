using InsuranceSemanticV2.Core.DTO;
using System.Net.Http.Json;

namespace InsuranceSemanticV2.IntegrationTests;

public class KpiServiceTests : IntegrationTestBase
{
    //[Fact]
    //public async Task GetKpis_ShouldReturnCorrectCounts()
    //{
    //    // Arrange - Create test leads with different temperatures
    //    var hotLead = new LeadRequest
    //    {
    //        FirstName = "Hot",
    //        LastName = "Lead",
    //        Email = "hot@test.com",
    //        PhoneNumber = "555-1111",
    //        PolicyType = "Life",
    //        Temperature = "Hot"
    //    };

    //    var warmLead = new LeadRequest
    //    {
    //        FirstName = "Warm",
    //        LastName = "Lead",
    //        Email = "warm@test.com",
    //        PhoneNumber = "555-2222",
    //        PolicyType = "Auto",
    //        Temperature = "Warm"
    //    };

    //    var coldLead = new LeadRequest
    //    {
    //        FirstName = "Cold",
    //        LastName = "Lead",
    //        Email = "cold@test.com",
    //        PhoneNumber = "555-3333",
    //        PolicyType = "Home",
    //        Temperature = "Cold"
    //    };

    //    await _client.PostAsJsonAsync("/api/leads", hotLead);
    //    await _client.PostAsJsonAsync("/api/leads", warmLead);
    //    await _client.PostAsJsonAsync("/api/leads", coldLead);

    //    // Act
    //    var kpis = await _client.GetFromJsonAsync<LeadKpiResponse>("/api/leads/kpis");

    //    // Assert
    //    Assert.NotNull(kpis);
    //    Assert.True(kpis.TotalLeads >= 3, $"Expected at least 3 leads, got {kpis.TotalLeads}");
    //    Assert.True(kpis.HotLeads >= 1, $"Expected at least 1 hot lead, got {kpis.HotLeads}");
    //    Assert.True(kpis.WarmLeads >= 1, $"Expected at least 1 warm lead, got {kpis.WarmLeads}");
    //    Assert.True(kpis.ColdLeads >= 1, $"Expected at least 1 cold lead, got {kpis.ColdLeads}");
    //}

    //[Fact]
    //public async Task GetKpis_ShouldIncludeConversionRates()
    //{
    //    // Act
    //    var kpis = await _client.GetFromJsonAsync<LeadKpiResponse>("/api/leads/kpis");

    //    // Assert
    //    Assert.NotNull(kpis);
    //    Assert.True(kpis.ConversionRate >= 0 && kpis.ConversionRate <= 100);
    //    Assert.True(kpis.AverageResponseTime >= TimeSpan.Zero);
    //}
}
