using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.IntegrationTests;

public class QualifiedLeadHandoffEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Handoff_ShouldQualifyLeadAndRecordSingleTransition()
    {
        var lead = await SeedLeadAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/leads/{lead.LeadId}/handoff",
            new QualifiedLeadHandoffRequest(lead.LeadId, 88));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QualifiedLeadHandoffResponse>();
        result.Should().NotBeNull();
        result!.LeadId.Should().Be(lead.LeadId);
        result.Status.Should().Be("Qualified");
        result.AlreadyAccepted.Should().BeFalse();
        result.AcceptedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        DbContext.ChangeTracker.Clear();
        var storedLead = await DbContext.Leads.SingleAsync(candidate => candidate.LeadId == lead.LeadId);
        storedLead.Status.Should().Be("Qualified");
        storedLead.QualificationScore.Should().Be(88);
        storedLead.FollowUpRequired.Should().BeTrue();

        var transition = await DbContext.LeadStatusHistories
            .SingleAsync(candidate => candidate.LeadId == lead.LeadId);
        transition.OldStatus.Should().Be("new");
        transition.NewStatus.Should().Be("Qualified");
    }

    [Fact]
    public async Task Handoff_WhenRepeated_ShouldBeIdempotent()
    {
        var lead = await SeedLeadAsync();
        var request = new QualifiedLeadHandoffRequest(lead.LeadId, 91);

        var firstResponse = await Client.PostAsJsonAsync($"/api/leads/{lead.LeadId}/handoff", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<QualifiedLeadHandoffResponse>();
        var secondResponse = await Client.PostAsJsonAsync(
            $"/api/leads/{lead.LeadId}/handoff",
            request with { QualificationScore = 12 });
        var second = await secondResponse.Content.ReadFromJsonAsync<QualifiedLeadHandoffResponse>();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        first!.AlreadyAccepted.Should().BeFalse();
        second!.AlreadyAccepted.Should().BeTrue();
        second.AcceptedAt.Should().Be(first.AcceptedAt);

        DbContext.ChangeTracker.Clear();
        (await DbContext.LeadStatusHistories.CountAsync(candidate => candidate.LeadId == lead.LeadId))
            .Should().Be(1);
        (await DbContext.Leads.SingleAsync(candidate => candidate.LeadId == lead.LeadId))
            .QualificationScore.Should().Be(91);
    }

    [Fact]
    public async Task Handoff_WhenLeadDoesNotExist_ShouldReturnNotFound()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/leads/99999/handoff",
            new QualifiedLeadHandoffRequest(99999, 75));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(43, 42, 75)]
    [InlineData(42, 42, -1)]
    [InlineData(42, 42, 101)]
    public async Task Handoff_WhenContractIsInvalid_ShouldReturnBadRequest(
        int routeLeadId,
        int requestLeadId,
        int score)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/leads/{routeLeadId}/handoff",
            new QualifiedLeadHandoffRequest(requestLeadId, score));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Lead> SeedLeadAsync()
    {
        var lead = new Lead
        {
            FullName = "Qualified Prospect",
            Email = "qualified@example.com",
            Phone = "555-0100",
            Status = "new",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        DbContext.Leads.Add(lead);
        await DbContext.SaveChangesAsync();
        return lead;
    }
}
