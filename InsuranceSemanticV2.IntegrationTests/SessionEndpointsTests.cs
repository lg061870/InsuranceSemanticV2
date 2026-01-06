using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.IntegrationTests;

public class SessionEndpointsTests : IntegrationTestBase
{
    private async Task<string> LoginAndGetToken(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = email,
            password = password
        });
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result!.Token;
    }

    [Fact]
    public async Task StartSession_CreatesSessionInDatabase()
    {
        // Arrange: Create agent and login
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Test Agent",
            Email = "test@example.com",
            Password = "pass123",
            Phone = "555-1234",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var token = await LoginAndGetToken("test@example.com", "pass123");

        // Act: Start session
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/start")
        {
            Content = JsonContent.Create(new
            {
                connectionId = "test-conn-123",
                ipAddress = "192.168.1.1",
                userAgent = "Mozilla/5.0 Test"
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        // Assert: Success
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SessionStartResponse>();
        result.Should().NotBeNull();
        result!.SessionId.Should().BeGreaterThan(0);

        // Verify session in database
        var session = await DbContext.AgentSessions.FindAsync(result.SessionId);
        session.Should().NotBeNull();
        session!.AgentId.Should().Be(agent.AgentId);
        session.ConnectionId.Should().Be("test-conn-123");
        session.Status.Should().Be("Online");
        session.IsActive.Should().BeTrue();
        session.IpAddress.Should().Be("192.168.1.1");
        session.UserAgent.Should().Be("Mozilla/5.0 Test");
    }

    [Fact]
    public async Task StartSession_WithoutConnectionId_ExtractsFromHttpContext()
    {
        // Arrange: Create agent and login
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "John Doe",
            Email = "john@example.com",
            Password = "pass123",
            Phone = "555-5678",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var token = await LoginAndGetToken("john@example.com", "pass123");

        // Act: Start session without explicitly providing IP/UserAgent
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/start")
        {
            Content = JsonContent.Create(new
            {
                connectionId = "auto-conn-456"
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        // Assert: Success with auto-extracted values
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SessionStartResponse>();

        var session = await DbContext.AgentSessions.FindAsync(result!.SessionId);
        session.Should().NotBeNull();
        session!.IpAddress.Should().NotBeNullOrEmpty(); // Should be auto-extracted
    }

    [Fact]
    public async Task StartSession_WithoutToken_Returns401()
    {
        // Act: Start session without authentication
        var response = await Client.PostAsJsonAsync("/api/sessions/start", new
        {
            connectionId = "test-conn"
        });

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Heartbeat_UpdatesLastActivityTime()
    {
        // Arrange: Create agent, login, and start session
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Jane Smith",
            Email = "jane@example.com",
            Password = "pass456",
            Phone = "555-9012",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var session = new AgentSession
        {
            AgentId = agent.AgentId,
            ConnectionId = "heartbeat-conn",
            LoginTime = DateTime.UtcNow.AddMinutes(-10),
            LastActivityTime = DateTime.UtcNow.AddMinutes(-10),
            Status = "Online",
            IsActive = true
        };
        DbContext.AgentSessions.Add(session);
        await DbContext.SaveChangesAsync();

        var token = await LoginAndGetToken("jane@example.com", "pass456");

        // Act: Send heartbeat
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/heartbeat");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        // Assert: Success
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify LastActivityTime was updated
        var updatedSession = await DbContext.AgentSessions.FindAsync(session.AgentSessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.LastActivityTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Heartbeat_WithoutToken_Returns401()
    {
        // Act: Send heartbeat without authentication
        var response = await Client.PostAsync("/api/sessions/heartbeat", null);

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndSession_SetsIsActiveFalse()
    {
        // Arrange: Create agent, login, and start session
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Bob Johnson",
            Email = "bob@example.com",
            Password = "pass789",
            Phone = "555-3456",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var session = new AgentSession
        {
            AgentId = agent.AgentId,
            ConnectionId = "end-conn",
            LoginTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            Status = "Online",
            IsActive = true
        };
        DbContext.AgentSessions.Add(session);
        await DbContext.SaveChangesAsync();

        var token = await LoginAndGetToken("bob@example.com", "pass789");

        // Act: End session
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/end");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        // Assert: Success
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify session ended
        var updatedSession = await DbContext.AgentSessions.FindAsync(session.AgentSessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.IsActive.Should().BeFalse();
        updatedSession.LogoutTime.Should().NotBeNull();
        updatedSession.LogoutTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EndSession_EndsMultipleSessions()
    {
        // Arrange: Create agent with multiple active sessions
        var agent = new Agent
        {
            CompanyId = 1,
            FullName = "Multi Session",
            Email = "multi@example.com",
            Password = "pass000",
            Phone = "555-7890",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.Add(agent);
        await DbContext.SaveChangesAsync();

        var session1 = new AgentSession
        {
            AgentId = agent.AgentId,
            ConnectionId = "multi-conn-1",
            LoginTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            Status = "Online",
            IsActive = true
        };
        var session2 = new AgentSession
        {
            AgentId = agent.AgentId,
            ConnectionId = "multi-conn-2",
            LoginTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            Status = "Online",
            IsActive = true
        };
        DbContext.AgentSessions.AddRange(session1, session2);
        await DbContext.SaveChangesAsync();

        var token = await LoginAndGetToken("multi@example.com", "pass000");

        // Act: End session
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/end");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        // Assert: Both sessions ended
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedSession1 = await DbContext.AgentSessions.FindAsync(session1.AgentSessionId);
        var updatedSession2 = await DbContext.AgentSessions.FindAsync(session2.AgentSessionId);

        updatedSession1!.IsActive.Should().BeFalse();
        updatedSession2!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveSessions_ReturnsOnlyActiveSessions()
    {
        // Arrange: Create multiple agents with various session states
        var agent1 = new Agent
        {
            CompanyId = 1,
            FullName = "Active Agent",
            Email = "active@example.com",
            Password = "pass1",
            Phone = "555-0001",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var agent2 = new Agent
        {
            CompanyId = 1,
            FullName = "Inactive Agent",
            Email = "inactive@example.com",
            Password = "pass2",
            Phone = "555-0002",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Agents.AddRange(agent1, agent2);
        await DbContext.SaveChangesAsync();

        // Active session
        var activeSession = new AgentSession
        {
            AgentId = agent1.AgentId,
            ConnectionId = "active-conn",
            LoginTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            Status = "Online",
            IsActive = true
        };
        // Inactive session
        var inactiveSession = new AgentSession
        {
            AgentId = agent2.AgentId,
            ConnectionId = "inactive-conn",
            LoginTime = DateTime.UtcNow.AddHours(-1),
            LastActivityTime = DateTime.UtcNow.AddHours(-1),
            LogoutTime = DateTime.UtcNow.AddMinutes(-30),
            Status = "Offline",
            IsActive = false
        };
        DbContext.AgentSessions.AddRange(activeSession, inactiveSession);
        await DbContext.SaveChangesAsync();

        var token = await LoginAndGetToken("active@example.com", "pass1");

        // Act: Get active sessions
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/sessions/active");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        // Assert: Only active session returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<List<object>>();
        sessions.Should().NotBeNull();
        sessions!.Count.Should().Be(1); // Only the active session
    }

    [Fact]
    public async Task GetActiveSessions_WithoutToken_Returns401()
    {
        // Act: Get active sessions without authentication
        var response = await Client.GetAsync("/api/sessions/active");

        // Assert: Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
