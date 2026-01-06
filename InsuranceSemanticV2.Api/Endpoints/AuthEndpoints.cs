using System.Security.Claims;
using InsuranceSemanticV2.Api.Hubs;
using InsuranceSemanticV2.Api.Services;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.DataContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Authentication");

        // POST /api/auth/login - JWT-based authentication
        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            AppDbContext db,
            JwtTokenService jwtService,
            ILoggerFactory loggerFactory,
            HttpContext httpContext) =>
        {
            var logger = loggerFactory.CreateLogger("Auth");
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            logger.LogInformation("[Auth] Login attempt for username: {Username} from IP: {IpAddress}",
                request.Username, ipAddress);

            // Validate credentials (demo: plain text password comparison)
            var agent = await db.Agents
                .FirstOrDefaultAsync(a => a.Email == request.Username && a.Password == request.Password);

            if (agent == null)
            {
                logger.LogWarning("[Auth] Failed login attempt for username: {Username} from IP: {IpAddress} - Invalid credentials",
                    request.Username, ipAddress);
                return Results.Unauthorized();
            }

            // Generate JWT token
            var token = jwtService.GenerateToken(agent);

            logger.LogInformation("[Auth] Successful login for Agent {AgentId} ({AgentName}) from IP: {IpAddress}",
                agent.AgentId, agent.FullName, ipAddress);

            // Return token and agent info
            var response = new LoginResponse
            {
                Token = token,
                Agent = new AgentDto
                {
                    AgentId = agent.AgentId,
                    FullName = agent.FullName,
                    Email = agent.Email,
                    Status = agent.Status,
                    AvatarUrl = agent.AvatarUrl
                }
            };

            return Results.Ok(response);
        });

        // POST /api/auth/logout - End agent sessions
        group.MapPost("/logout", [Authorize] async (
            ClaimsPrincipal user,
            AppDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Auth");
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim == null || !int.TryParse(agentIdClaim, out var agentId))
            {
                logger.LogWarning("[Auth] Logout attempt with invalid AgentId claim");
                return Results.Unauthorized();
            }

            // End all active sessions for this agent
            var activeSessions = await db.AgentSessions
                .Where(s => s.AgentId == agentId && s.IsActive)
                .ToListAsync();

            foreach (var session in activeSessions)
            {
                session.LogoutTime = DateTime.UtcNow;
                session.IsActive = false;
                session.Status = "Offline";
            }

            await db.SaveChangesAsync();

            logger.LogInformation("[Auth] Agent {AgentId} logged out - {SessionCount} session(s) ended",
                agentId, activeSessions.Count);

            return Results.Ok(new { success = true });
        });

        // GET /api/auth/me - Get current agent info
        group.MapPost("/me", [Authorize] async (
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim == null || !int.TryParse(agentIdClaim, out var agentId))
            {
                return Results.Unauthorized();
            }

            var agent = await db.Agents.FindAsync(agentId);
            if (agent == null)
            {
                return Results.NotFound();
            }

            // Get current session status
            var activeSession = await db.AgentSessions
                .Where(s => s.AgentId == agentId && s.IsActive)
                .OrderByDescending(s => s.LoginTime)
                .FirstOrDefaultAsync();

            var agentDto = new AgentDto
            {
                AgentId = agent.AgentId,
                FullName = agent.FullName,
                Email = agent.Email,
                Status = activeSession?.Status ?? agent.Status,
                AvatarUrl = agent.AvatarUrl
            };

            return Results.Ok(agentDto);
        });

        // PUT /api/auth/status - Update agent status
        group.MapPut("/status", [Authorize] async (
            [FromBody] SetStatusRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            IHubContext<LeadsHub> hubContext) =>
        {
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim == null || !int.TryParse(agentIdClaim, out var agentId))
            {
                return Results.Unauthorized();
            }

            // Update status in active sessions
            var activeSessions = await db.AgentSessions
                .Where(s => s.AgentId == agentId && s.IsActive)
                .ToListAsync();

            foreach (var session in activeSessions)
            {
                session.Status = request.Status;
                session.LastActivityTime = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            // Broadcast status change via SignalR
            await hubContext.Clients.All.SendAsync("AgentStatusChanged", agentId, request.Status);

            return Results.Ok(new { success = true, status = request.Status });
        });

        return group;
    }
}
