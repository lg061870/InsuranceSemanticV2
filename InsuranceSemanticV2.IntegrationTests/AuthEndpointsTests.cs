using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.IntegrationTests;

public class AuthEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange: Seed an agent
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Test Agent",
            Email = "test@example.com",
            Password = "testpass123",
            Phone = "555-1234",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        // Act: Login
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test@example.com",
            password = "testpass123"
        });

        // Assert: Success with token
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.Agent.Should().NotBeNull();
        result.Agent.Email.Should().Be("test@example.com");
        result.Agent.FullName.Should().Be("Test Agent");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        // Arrange: Seed an agent with different password
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Test Agent",
            Email = "test@example.com",
            Password = "correctpass",
            Phone = "555-1234",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        // Act: Login with wrong password
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "test@example.com",
            password = "wrongpass"
        });

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_Returns401()
    {
        // Act: Login with non-existent user
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "nonexistent@example.com",
            password = "anypass"
        });

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentAgent_WithValidToken_ReturnsAgent()
    {
        // Arrange: Login to get token
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "John Doe",
            Email = "john@example.com",
            Password = "pass123",
            Phone = "555-1234",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "john@example.com",
            password = "pass123"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Act: Get current agent
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        var response = await Client.SendAsync(request);

        // Assert: Returns agent info
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var agentDto = await response.Content.ReadFromJsonAsync<AgentDto>();
        agentDto.Should().NotBeNull();
        agentDto!.Email.Should().Be("john@example.com");
        agentDto.FullName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetCurrentAgent_WithoutToken_Returns401()
    {
        // Act: Call /me without token
        var response = await Client.PostAsync("/api/auth/me", null);

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithValidToken_EndsSessions()
    {
        // Arrange: Login and create session
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Jane Smith",
            Email = "jane@example.com",
            Password = "pass456",
            Phone = "555-5678",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "jane@example.com",
            password = "pass456"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create an active session
        var session = new AgentSession
        {
            AgentId = agent.AgentId,
            ConnectionId = "test-conn-123",
            LoginTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            Status = "Online",
            IsActive = true
        };
        DbContext.AgentSessions.Add(session);
        await DbContext.SaveChangesAsync();

        // Act: Logout
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        var response = await Client.SendAsync(request);

        // Assert: Success
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify session ended in database
        var updatedSession = await DbContext.AgentSessions.FindAsync(session.AgentSessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.IsActive.Should().BeFalse();
        updatedSession.LogoutTime.Should().NotBeNull();
        updatedSession.Status.Should().Be("Offline");
    }

    [Fact]
    public async Task UpdateStatus_WithValidToken_UpdatesSessionStatus()
    {
        // Arrange: Login and create session
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Bob Johnson",
            Email = "bob@example.com",
            Password = "pass789",
            Phone = "555-9012",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "bob@example.com",
            password = "pass789"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Create an active session
        var session = new AgentSession
        {
            AgentId = agent.AgentId,
            ConnectionId = "test-conn-456",
            LoginTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            Status = "Online",
            IsActive = true
        };
        DbContext.AgentSessions.Add(session);
        await DbContext.SaveChangesAsync();

        // Act: Update status to "Away"
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/status")
        {
            Content = JsonContent.Create(new { status = "Away" })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        var response = await Client.SendAsync(request);

        // Assert: Success
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status updated in database
        var updatedSession = await DbContext.AgentSessions.FindAsync(session.AgentSessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be("Away");
        updatedSession.LastActivityTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateStatus_WithoutToken_Returns401()
    {
        // Act: Update status without token
        var response = await Client.PutAsJsonAsync("/api/auth/status", new { status = "Away" });

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
