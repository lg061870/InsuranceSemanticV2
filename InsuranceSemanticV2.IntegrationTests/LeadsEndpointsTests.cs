using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InsuranceSemanticV2.Core.DTO;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.IntegrationTests;

public class LeadsEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateLead_ShouldCreateLeadInDatabase_AndReturnCreatedResponse()
    {
        // Arrange
        var leadRequest = new LeadRequest
        {
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234",
            Status = "new",
            LeadSource = "website",
            Language = "English",
            LeadIntent = "life insurance",
            InterestLevel = "high",
            QualificationScore = 85,
            FollowUpRequired = true,
            Notes = "Interested in $500k policy"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/leads", leadRequest);

        // Assert - HTTP Response
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdLead = await response.Content.ReadFromJsonAsync<LeadResponse>();
        createdLead.Should().NotBeNull();
        createdLead!.LeadId.Should().BeGreaterThan(0);

        // Assert - Database Record
        var dbLead = await DbContext.Leads
            .FirstOrDefaultAsync(l => l.Email == "john.doe@example.com");

        dbLead.Should().NotBeNull();
        dbLead!.FullName.Should().Be("John Doe");
        dbLead.Email.Should().Be("john.doe@example.com");
        dbLead.Phone.Should().Be("555-1234");
        dbLead.Status.Should().Be("new");
        dbLead.LeadSource.Should().Be("website");
        dbLead.Language.Should().Be("English");
        dbLead.LeadIntent.Should().Be("life insurance");
        dbLead.InterestLevel.Should().Be("high");
        dbLead.QualificationScore.Should().Be(85);
        dbLead.FollowUpRequired.Should().BeTrue();
        dbLead.Notes.Should().Be("Interested in $500k policy");
        dbLead.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        dbLead.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetLead_ShouldReturnLeadFromDatabase_WithCorrectMapping()
    {
        // Arrange - Create a lead in the database
        var lead = new Data.Entities.Lead
        {
            FullName = "Jane Smith",
            Email = "jane.smith@example.com",
            Phone = "555-5678",
            Status = "contacted",
            LeadSource = "referral",
            Language = "Spanish",
            LeadIntent = "whole life",
            InterestLevel = "medium",
            QualificationScore = 70,
            FollowUpRequired = false,
            Notes = "Prefers evening calls",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        DbContext.Leads.Add(lead);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/leads/{lead.LeadId}");

        // Assert - HTTP Response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var leadResponse = await response.Content.ReadFromJsonAsync<LeadResponse>();
        leadResponse.Should().NotBeNull();
        leadResponse!.LeadId.Should().Be(lead.LeadId);
        leadResponse.Payload.Should().HaveCount(1);

        // Assert - AutoMapper mapping is correct
        var mappedLead = leadResponse.Payload[0];
        mappedLead.FullName.Should().Be("Jane Smith");
        mappedLead.Email.Should().Be("jane.smith@example.com");
        mappedLead.Phone.Should().Be("555-5678");
        mappedLead.Status.Should().Be("contacted");
        mappedLead.LeadSource.Should().Be("referral");
        mappedLead.Language.Should().Be("Spanish");
        mappedLead.LeadIntent.Should().Be("whole life");
        mappedLead.InterestLevel.Should().Be("medium");
        mappedLead.QualificationScore.Should().Be(70);
        mappedLead.FollowUpRequired.Should().BeFalse();
        mappedLead.Notes.Should().Be("Prefers evening calls");
    }

    [Fact]
    public async Task UpdateLead_ShouldUpdateDatabaseRecord_AndPreserveCorrectFields()
    {
        // Arrange - Create initial lead
        var lead = new Data.Entities.Lead
        {
            FullName = "Bob Johnson",
            Email = "bob@example.com",
            Phone = "555-9999",
            Status = "new",
            QualificationScore = 50,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        DbContext.Leads.Add(lead);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = lead.CreatedAt;
        var originalLeadId = lead.LeadId;

        // Act - Update the lead
        var updateRequest = new LeadRequest
        {
            FullName = "Bob Johnson Jr.",
            Email = "bob@example.com",
            Phone = "555-8888",
            Status = "qualified",
            QualificationScore = 95,
            FollowUpRequired = true,
            Notes = "Ready to purchase"
        };

        var response = await Client.PutAsync($"/api/leads/{lead.LeadId}",
            JsonContent.Create(updateRequest));

        // Assert - HTTP Response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Database was updated correctly
        var updatedLead = await DbContext.Leads.FindAsync(originalLeadId);
        updatedLead.Should().NotBeNull();
        updatedLead!.LeadId.Should().Be(originalLeadId); // ID unchanged
        updatedLead.FullName.Should().Be("Bob Johnson Jr.");
        updatedLead.Email.Should().Be("bob@example.com");
        updatedLead.Phone.Should().Be("555-8888");
        updatedLead.Status.Should().Be("qualified");
        updatedLead.QualificationScore.Should().Be(95);
        updatedLead.FollowUpRequired.Should().BeTrue();
        updatedLead.Notes.Should().Be("Ready to purchase");
        updatedLead.CreatedAt.Should().Be(originalCreatedAt); // CreatedAt preserved
    }

    [Fact]
    public async Task GetLead_WhenLeadDoesNotExist_ShouldReturn404()
    {
        // Act
        var response = await Client.GetAsync("/api/leads/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateLead_WhenLeadDoesNotExist_ShouldReturn404()
    {
        // Arrange
        var updateRequest = new LeadRequest
        {
            FullName = "Nobody",
            Email = "nobody@example.com",
            Phone = "000-0000",
            Status = "new"
        };

        // Act
        var response = await Client.PutAsync("/api/leads/99999",
            JsonContent.Create(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateMultipleLeads_ShouldAllBeStoredInDatabase()
    {
        // Arrange
        var leads = new[]
        {
            new LeadRequest { FullName = "Lead 1", Email = "lead1@test.com", Phone = "111", Status = "new" },
            new LeadRequest { FullName = "Lead 2", Email = "lead2@test.com", Phone = "222", Status = "new" },
            new LeadRequest { FullName = "Lead 3", Email = "lead3@test.com", Phone = "333", Status = "new" }
        };

        // Act
        foreach (var lead in leads)
        {
            await Client.PostAsJsonAsync("/api/leads", lead);
        }

        // Assert
        var dbLeads = await DbContext.Leads.ToListAsync();
        dbLeads.Should().HaveCount(3);
        dbLeads.Select(l => l.FullName).Should().Contain(new[] { "Lead 1", "Lead 2", "Lead 3" });
    }
}
