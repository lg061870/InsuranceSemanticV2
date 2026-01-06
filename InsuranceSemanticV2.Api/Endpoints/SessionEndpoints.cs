using System.Security.Claims;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sessions").WithTags("Session Management");

        // POST /api/sessions/start - Create new agent session
        group.MapPost("/start", [Authorize] async (
            [FromBody] SessionStartRequest request,
            ClaimsPrincipal user,
            AppDbContext db,
            HttpContext httpContext,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Session");
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim == null || !int.TryParse(agentIdClaim, out var agentId))
            {
                logger.LogWarning("[Session] Start session attempt with invalid AgentId claim");
                return Results.Unauthorized();
            }

            // Get IP address and user agent from request if not provided
            var ipAddress = request.IpAddress
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "Unknown";
            var userAgent = request.UserAgent
                ?? httpContext.Request.Headers["User-Agent"].ToString()
                ?? "Unknown";

            // Create new session
            var session = new AgentSession
            {
                AgentId = agentId,
                ConnectionId = request.ConnectionId,
                LoginTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                Status = "Online",
                IsActive = true,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            db.AgentSessions.Add(session);
            await db.SaveChangesAsync();

            logger.LogInformation("[Session] Started session {SessionId} for Agent {AgentId} with ConnectionId {ConnectionId} from IP {IpAddress}",
                session.AgentSessionId, agentId, request.ConnectionId, ipAddress);

            return Results.Ok(new SessionStartResponse
            {
                SessionId = session.AgentSessionId
            });
        });

        // POST /api/sessions/heartbeat - Update last activity time
        group.MapPost("/heartbeat", [Authorize] async (
            ClaimsPrincipal user,
            AppDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Session");
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim == null || !int.TryParse(agentIdClaim, out var agentId))
            {
                logger.LogWarning("[Session] Heartbeat attempt with invalid AgentId claim");
                return Results.Unauthorized();
            }

            // Update last activity time for all active sessions
            var activeSessions = await db.AgentSessions
                .Where(s => s.AgentId == agentId && s.IsActive)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var session in activeSessions)
            {
                session.LastActivityTime = now;

                // Auto-update status based on inactivity (from config)
                // This would be enhanced with configuration values
                var inactiveMinutes = (now - session.LastActivityTime).TotalMinutes;
                if (inactiveMinutes > 15 && session.Status != "Offline")
                {
                    session.Status = "Offline";
                    session.IsActive = false;
                }
                else if (inactiveMinutes > 5 && session.Status == "Online")
                {
                    session.Status = "Away";
                }
            }

            await db.SaveChangesAsync();

            logger.LogDebug("[Session] Heartbeat received from Agent {AgentId} - {SessionCount} session(s) updated",
                agentId, activeSessions.Count);

            return Results.Ok(new { success = true, timestamp = now });
        });

        // POST /api/sessions/end - End current session
        group.MapPost("/end", [Authorize] async (
            ClaimsPrincipal user,
            AppDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Session");
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim == null || !int.TryParse(agentIdClaim, out var agentId))
            {
                logger.LogWarning("[Session] End session attempt with invalid AgentId claim");
                return Results.Unauthorized();
            }

            // End all active sessions for this agent
            var activeSessions = await db.AgentSessions
                .Where(s => s.AgentId == agentId && s.IsActive)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var session in activeSessions)
            {
                session.LogoutTime = now;
                session.IsActive = false;
                session.Status = "Offline";
            }

            await db.SaveChangesAsync();

            logger.LogInformation("[Session] Ended {SessionCount} session(s) for Agent {AgentId}",
                activeSessions.Count, agentId);

            return Results.Ok(new { success = true, endedSessions = activeSessions.Count });
        });

        // GET /api/sessions/active - Get all active agent sessions
        group.MapGet("/active", [Authorize] async (AppDbContext db) =>
        {
            var activeSessions = await db.AgentSessions
                .Include(s => s.Agent)
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.LoginTime)
                .Select(s => new
                {
                    sessionId = s.AgentSessionId,
                    agentId = s.AgentId,
                    agentName = s.Agent != null ? s.Agent.FullName : "Unknown",
                    agentEmail = s.Agent != null ? s.Agent.Email : "Unknown",
                    connectionId = s.ConnectionId,
                    loginTime = s.LoginTime,
                    lastActivityTime = s.LastActivityTime,
                    status = s.Status,
                    ipAddress = s.IpAddress,
                    userAgent = s.UserAgent
                })
                .ToListAsync();

            return Results.Ok(activeSessions);
        });

        return group;
    }
}
