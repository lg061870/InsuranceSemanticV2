using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class AgentsEndpoints {
    public static RouteGroupBuilder MapAgentEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/agents")
                          .WithTags("Agents");

        // ============================================================
        //  AGENT CRUD
        // ============================================================

        group.MapGet("/", async (AppDbContext db) =>
        {
            var agents = await db.Agents.ToListAsync();

            return Results.Ok(new AgentResponse {
                Payload = agents.Select(a => new AgentRequest {
                    CompanyId = a.CompanyId,
                    FullName = a.FullName,
                    Email = a.Email,
                    Phone = a.Phone,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,

                    // Custom live-agent UI fields
                    AvatarUrl = a.AvatarUrl,
                    Specialty = a.Specialty,
                    Rating = a.Rating,
                    Calls = a.Calls,
                    AvgMinutes = a.AvgMinutes,
                    IsAvailable = a.IsAvailable,
                    StatusLabel = a.StatusLabel,
                    StatusColor = a.StatusColor

                }).ToList()
            });
        });

        group.MapGet("/{agentId:int}", async (int agentId, AppDbContext db) =>
        {
            var a = await db.Agents.FindAsync(agentId);
            if (a is null) return Results.NotFound();

            return Results.Ok(new AgentResponse {
                AgentId = a.AgentId,
                Payload = new()
                {
            new AgentRequest
            {
                CompanyId = a.CompanyId,
                FullName = a.FullName,
                Email = a.Email,
                Phone = a.Phone,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,

                // Custom UI fields
                AvatarUrl = a.AvatarUrl,
                Specialty = a.Specialty,
                Rating = a.Rating,
                Calls = a.Calls,
                AvgMinutes = a.AvgMinutes,
                IsAvailable = a.IsAvailable,
                StatusLabel = a.StatusLabel,
                StatusColor = a.StatusColor
            }
        }
            });
        });

        group.MapPost("/", async (AgentRequest dto, AppDbContext db) =>
        {
            var entity = new Agent {
                CompanyId = dto.CompanyId,
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                AvatarUrl = dto.AvatarUrl,
                Specialty = dto.Specialty,
                Rating = dto.Rating,
                Calls = dto.Calls,
                AvgMinutes = dto.AvgMinutes,
                IsAvailable = dto.IsAvailable,
                StatusLabel = dto.StatusLabel,
                StatusColor = dto.StatusColor
            };

            db.Agents.Add(entity);
            await db.SaveChangesAsync();

            return Results.Created($"/api/agents/{entity.AgentId}",
                new AgentResponse {
                    AgentId = entity.AgentId,
                    Payload = new List<AgentRequest> { dto }
                });
        });

        group.MapPut("/{agentId:int}", async (int agentId, AgentRequest dto, AppDbContext db) =>
        {
            var entity = await db.Agents.FindAsync(agentId);
            if (entity is null) return Results.NotFound();

            entity.CompanyId = dto.CompanyId;
            entity.FullName = dto.FullName;
            entity.Email = dto.Email;
            entity.Phone = dto.Phone;
            entity.Status = dto.Status;
            entity.UpdatedAt = DateTime.UtcNow;

            // Custom UI fields
            entity.AvatarUrl = dto.AvatarUrl;
            entity.Specialty = dto.Specialty;
            entity.Rating = dto.Rating;
            entity.Calls = dto.Calls;
            entity.AvgMinutes = dto.AvgMinutes;
            entity.IsAvailable = dto.IsAvailable;
            entity.StatusLabel = dto.StatusLabel;
            entity.StatusColor = dto.StatusColor;

            await db.SaveChangesAsync();

            return Results.Ok(new AgentResponse {
                AgentId = entity.AgentId,
                Payload = new List<AgentRequest> { dto }
            });
        });

        group.MapDelete("/{agentId:int}", async (int agentId, AppDbContext db) =>
        {
            var entity = await db.Agents.FindAsync(agentId);
            if (entity is null) return Results.NotFound();

            db.Agents.Remove(entity);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Agent deleted." });
        });


        // ============================================================
        //  AGENT LEADS
        // ============================================================

        group.MapGet("/{agentId:int}/leads", async (int agentId, AppDbContext db) => {
            var leads = await db.Leads
                .Where(l => l.AssignedAgentId == agentId)
                .ToListAsync();

            return Results.Ok(leads);
        });

        // ============================================================
        //  AGENT LICENSES
        // ============================================================

        group.MapGet("/{agentId:int}/licenses", async (int agentId, AppDbContext db) => {
            var items = await db.AgentLicenses
                .Where(x => x.AgentId == agentId)
                .ToListAsync();

            return Results.Ok(new AgentLicenseResponse {
                Payload = items.Select(x => new AgentLicenseRequest {
                    AgentId = x.AgentId,
                    LicenseNumber = x.LicenseNumber,
                    State = x.State,
                    ExpiresOn = x.ExpiresOn
                }).ToList(),
                LicenseId = 0
            });
        });

        group.MapPost("/{agentId:int}/licenses", async (int agentId, AgentLicenseRequest req, AppDbContext db) => {
            var entity = new AgentLicense {
                AgentId = agentId,
                State = req.State,
                LicenseNumber = req.LicenseNumber,
                ExpiresOn = req.ExpiresOn
            };

            db.AgentLicenses.Add(entity);
            await db.SaveChangesAsync();

            return Results.Created($"/api/agents/{agentId}/licenses/{entity.LicenseId}",
                new AgentLicenseResponse {
                    LicenseId = entity.LicenseId,
                    Payload = new List<AgentLicenseRequest> { req }
                });
        });

        // ============================================================
        //  AGENT CARRIER APPOINTMENTS
        // ============================================================

        group.MapGet("/{agentId:int}/appointments", async (int agentId, AppDbContext db) => {
            var data = await db.AgentCarrierAppointments
                .Where(x => x.AgentId == agentId)
                .Include(x => x.Carrier)
                .ToListAsync();

            return Results.Ok(new AgentCarrierAppointmentResponse {
                Payload = data.Select(a => new AgentCarrierAppointmentRequest {
                    AgentId = a.AgentId,
                    CarrierId = a.CarrierId,
                    Status = a.Status
                }).ToList()
            });
        });

        group.MapPost("/{agentId:int}/appointments", async (int agentId, AgentCarrierAppointmentRequest req, AppDbContext db) => {
            var entity = new AgentCarrierAppointment {
                AgentId = agentId,
                CarrierId = req.CarrierId,
                Status = req.Status
            };

            db.AgentCarrierAppointments.Add(entity);
            await db.SaveChangesAsync();

            return Results.Created($"/api/agents/{agentId}/appointments/{entity.AppointmentId}",
                new AgentCarrierAppointmentResponse {
                    AppointmentId = entity.AppointmentId,
                    Payload = new List<AgentCarrierAppointmentRequest> { req }
                });
        });

        // ============================================================
        //  AGENT SESSIONS
        // ============================================================

        group.MapGet("/{agentId:int}/sessions", async (int agentId, AppDbContext db) => {
            var sessions = await db.AgentSessions
                .Where(s => s.AgentId == agentId)
                .OrderByDescending(s => s.LoginTime)
                .ToListAsync();

            return Results.Ok(new AgentSessionResponse {
                Payload = sessions.Select(s => new AgentSessionRequest {
                    AgentId = s.AgentId,
                    StartedAt = s.LoginTime,
                    EndedAt = s.LogoutTime ?? default(DateTime) // Fixes CS0266 and CS8629
                }).ToList()
            });
        });

        group.MapPost("/{agentId:int}/sessions", async (int agentId, AgentSessionRequest req, AppDbContext db) => {
            var entity = new AgentSession {
                AgentId = agentId,
                LoginTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                LogoutTime = null,
                ConnectionId = "",  // Will be set when SignalR connection established
                Status = "Online",
                IsActive = true
            };

            db.AgentSessions.Add(entity);
            await db.SaveChangesAsync();

            return Results.Created($"/api/agents/{agentId}/sessions/{entity.AgentSessionId}",
                new AgentSessionResponse {
                    SessionId = entity.AgentSessionId,
                    Payload = new List<AgentSessionRequest> { req }
                });
        });

        group.MapPost("/{agentId:int}/sessions/{sessionId:int}/end",
            async (int agentId, int sessionId, AppDbContext db) =>
            {
                var session = await db.AgentSessions.FindAsync(sessionId);
                if (session is null || session.AgentId != agentId)
                    return Results.NotFound();

                session.LogoutTime = DateTime.UtcNow;
                session.IsActive = false;
                await db.SaveChangesAsync();

                return Results.Ok(session);
            });

        // ============================================================
        //  CONTACT ATTEMPTS (NO AgentId in entity)
        // ============================================================

        group.MapPost("/{agentId:int}/contact-attempts", async (
            int agentId,
            ContactAttemptRequest req,
            AppDbContext db
        ) => {
            var entity = new ContactAttempt {
                LeadId = req.LeadId,
                AttemptTime = req.AttemptTime,
                Method = req.Method,
                Outcome = req.Outcome
            };

            db.ContactAttempts.Add(entity);
            await db.SaveChangesAsync();

            return Results.Ok(new ContactAttemptResponse {
                AttemptId = entity.AttemptId,
                Payload = new List<ContactAttemptRequest> { req },
                Message = "Contact attempt logged."
            });
        });

        return group;
    }
}
