using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.IntegrationTests;

public class AgentsEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateAgent_ShouldCreateAgentInDatabase_WithAllFields()
    {
        // Arrange
        var company = new Company { Name = "Test Insurance Co" };
        DbContext.Companies.Add(company);
        await DbContext.SaveChangesAsync();

        var agentRequest = new AgentRequest
        {
            CompanyId = company.CompanyId,
            FullName = "Sarah Connor",
            Email = "sarah.connor@insurance.com",
            Phone = "555-7890",
            Status = "active",
            AvatarUrl = "https://example.com/avatar.jpg",
            Specialty = "Life Insurance",
            Rating = 4.8,
            Calls = 150,
            AvgMinutes = 12,
            IsAvailable = true,
            StatusLabel = "Available Now",
            StatusColor = "green"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/agents", agentRequest);

        // Assert - HTTP Response
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - Database Record with complete field mapping
        var dbAgent = await DbContext.Agents
            .FirstOrDefaultAsync(a => a.Email == "sarah.connor@insurance.com");

        dbAgent.Should().NotBeNull();
        dbAgent!.CompanyId.Should().Be(company.CompanyId);
        dbAgent.FullName.Should().Be("Sarah Connor");
        dbAgent.Email.Should().Be("sarah.connor@insurance.com");
        dbAgent.Phone.Should().Be("555-7890");
        dbAgent.Status.Should().Be("active");
        dbAgent.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        dbAgent.Specialty.Should().Be("Life Insurance");
        dbAgent.Rating.Should().Be(4.8);
        dbAgent.Calls.Should().Be(150);
        dbAgent.AvgMinutes.Should().Be(12);
        dbAgent.IsAvailable.Should().BeTrue();
        dbAgent.StatusLabel.Should().Be("Available Now");
        dbAgent.StatusColor.Should().Be("green");
        dbAgent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        dbAgent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAgents_ShouldReturnAllAgents_WithCorrectMapping()
    {
        // Arrange
        var company = new Company { Name = "ABC Insurance" };
        DbContext.Companies.Add(company);
        await DbContext.SaveChangesAsync();

        var agents = new[]
        {
            new Agent
            {
                CompanyId = company.CompanyId,
                FullName = "Agent One",
                Email = "agent1@test.com",
                Phone = "111-1111",
                Status = "active",
                Specialty = "Health",
                Rating = 4.5,
                Calls = 100,
                AvgMinutes = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Agent
            {
                CompanyId = company.CompanyId,
                FullName = "Agent Two",
                Email = "agent2@test.com",
                Phone = "222-2222",
                Status = "busy",
                Specialty = "Auto",
                Rating = 4.9,
                Calls = 200,
                AvgMinutes = 15,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        DbContext.Agents.AddRange(agents);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentResponse = await response.Content.ReadFromJsonAsync<AgentResponse>();
        agentResponse.Should().NotBeNull();
        agentResponse!.Payload.Should().HaveCount(2);
        agentResponse.Payload.Select(a => a.FullName).Should().Contain(new[] { "Agent One", "Agent Two" });
    }

    [Fact]
    public async Task UpdateAgent_ShouldUpdateAllFields_AndPreserveCreatedAt()
    {
        // Arrange
        var company = new Company { Name = "XYZ Corp" };
        DbContext.Companies.Add(company);
        await DbContext.SaveChangesAsync();

        var agent = new Agent
        {
            CompanyId = company.CompanyId,
            FullName = "John Doe",
            Email = "john@test.com",
            Phone = "555-0000",
            Status = "active",
            Rating = 3.0,
            Calls = 50,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1)
        };

        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = agent.CreatedAt;

        // Act
        var updateRequest = new AgentRequest
        {
            CompanyId = company.CompanyId,
            FullName = "John Doe Updated",
            Email = "john.updated@test.com",
            Phone = "555-9999",
            Status = "inactive",
            Rating = 4.7,
            Calls = 250,
            AvgMinutes = 20,
            Specialty = "Commercial",
            IsAvailable = false,
            StatusLabel = "On Break",
            StatusColor = "yellow"
        };

        var response = await Client.PutAsync($"/api/agents/{agent.AgentId}",
            JsonContent.Create(updateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedAgent = await DbContext.Agents.FindAsync(agent.AgentId);
        updatedAgent!.FullName.Should().Be("John Doe Updated");
        updatedAgent.Email.Should().Be("john.updated@test.com");
        updatedAgent.Phone.Should().Be("555-9999");
        updatedAgent.Status.Should().Be("inactive");
        updatedAgent.Rating.Should().Be(4.7);
        updatedAgent.Calls.Should().Be(250);
        updatedAgent.AvgMinutes.Should().Be(20);
        updatedAgent.Specialty.Should().Be("Commercial");
        updatedAgent.IsAvailable.Should().BeFalse();
        updatedAgent.StatusLabel.Should().Be("On Break");
        updatedAgent.StatusColor.Should().Be("yellow");
        updatedAgent.CreatedAt.Should().Be(originalCreatedAt); // Should not change
        updatedAgent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeleteAgent_ShouldRemoveFromDatabase()
    {
        // Arrange
        var company = new Company { Name = "Delete Test Co" };
        DbContext.Companies.Add(company);
        await DbContext.SaveChangesAsync();

        var agent = new Agent
        {
            CompanyId = company.CompanyId,
            FullName = "Delete Me",
            Email = "delete@test.com",
            Phone = "000-0000",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var agentId = agent.AgentId;

        // Act
        var response = await Client.DeleteAsync($"/api/agents/{agentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deletedAgent = await DbContext.Agents.FindAsync(agentId);
        deletedAgent.Should().BeNull();
    }

    [Fact]
    public async Task GetAgentLeads_ShouldReturnAssignedLeads()
    {
        // Arrange
        var company = new Company { Name = "Leads Test Co" };
        DbContext.Companies.Add(company);
        await DbContext.SaveChangesAsync();

        var agent = new Agent
        {
            CompanyId = company.CompanyId,
            FullName = "Lead Handler",
            Email = "handler@test.com",
            Phone = "555-LEAD",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var leads = new[]
        {
            new Lead
            {
                FullName = "Customer 1",
                Email = "cust1@test.com",
                Phone = "111",
                Status = "new",
                AssignedAgentId = agent.AgentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Lead
            {
                FullName = "Customer 2",
                Email = "cust2@test.com",
                Phone = "222",
                Status = "contacted",
                AssignedAgentId = agent.AgentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        DbContext.Leads.AddRange(leads);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/agents/{agent.AgentId}/leads");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var returnedLeads = await response.Content.ReadFromJsonAsync<List<Lead>>();
        returnedLeads.Should().HaveCount(2);
        returnedLeads!.Select(l => l.FullName).Should().Contain(new[] { "Customer 1", "Customer 2" });
    }

    [Fact]
    public async Task AddAgentLicense_ShouldCreateLicenseRecord()
    {
        // Arrange
        var company = new Company { Name = "License Test Co" };
        DbContext.Companies.Add(company);
        await DbContext.SaveChangesAsync();

        var agent = new Agent
        {
            CompanyId = company.CompanyId,
            FullName = "Licensed Agent",
            Email = "licensed@test.com",
            Phone = "555-LIC",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var licenseRequest = new AgentLicenseRequest
        {
            AgentId = agent.AgentId,
            State = "CA",
            LicenseNumber = "CA12345",
            ExpiresOn = DateTime.UtcNow.AddYears(2)
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/agents/{agent.AgentId}/licenses", licenseRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dbLicense = await DbContext.AgentLicenses
            .FirstOrDefaultAsync(l => l.AgentId == agent.AgentId && l.State == "CA");

        dbLicense.Should().NotBeNull();
        dbLicense!.LicenseNumber.Should().Be("CA12345");
        dbLicense.State.Should().Be("CA");
        dbLicense.ExpiresOn.Should().BeCloseTo(DateTime.UtcNow.AddYears(2), TimeSpan.FromSeconds(5));
    }
}
