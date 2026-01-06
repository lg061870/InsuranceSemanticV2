using System.Collections.Concurrent;
using InsuranceSemanticV2.Data.DataContext;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time lead updates to LiveAgentConsole clients
/// </summary>
public class LeadsHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ILogger<LeadsHub> _logger;

    // In-memory tracking of ConnectionId -> AgentId mappings
    private static readonly ConcurrentDictionary<string, int> _agentConnections = new();

    public LeadsHub(AppDbContext db, ILogger<LeadsHub> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    /// <summary>
    /// Notify all connected clients that a new lead was created
    /// </summary>
    public async Task NotifyLeadCreated(int leadId) {
        await Clients.All.SendAsync("LeadCreated", leadId);
    }
    
    /// <summary>
    /// Notify all connected clients that a lead was updated
    /// </summary>
    public async Task NotifyLeadUpdated(int leadId) {
        await Clients.All.SendAsync("LeadUpdated", leadId);
    }
    
    /// <summary>
    /// Notify all connected clients that a lead's profile was updated (progress changed)
    /// </summary>
    public async Task NotifyProfileUpdated(int leadId) {
        await Clients.All.SendAsync("ProfileUpdated", leadId);
    }
    
    /// <summary>
    /// Notify all connected clients to refresh KPIs
    /// </summary>
    public async Task NotifyKpisChanged() {
        await Clients.All.SendAsync("KpisChanged");
    }
    
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("[LeadsHub] Client connected: {ConnectionId}", connectionId);

        // Try to get AgentId from JWT claims
        var agentIdClaim = Context.User?.FindFirst("AgentId")?.Value;
        if (agentIdClaim != null && int.TryParse(agentIdClaim, out var agentId))
        {
            // Store the connection mapping
            _agentConnections[connectionId] = agentId;

            // Update AgentSession with this connectionId (if session exists)
            var activeSession = await _db.AgentSessions
                .Where(s => s.AgentId == agentId && s.IsActive)
                .OrderByDescending(s => s.LoginTime)
                .FirstOrDefaultAsync();

            if (activeSession != null)
            {
                activeSession.ConnectionId = connectionId;
                activeSession.LastActivityTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogInformation("[LeadsHub] Agent {AgentId} connected with ConnectionId {ConnectionId}",
                    agentId, connectionId);

                // Get agent info
                var agent = await _db.Agents.FindAsync(agentId);
                if (agent != null)
                {
                    // Broadcast that agent is now online
                    await Clients.All.SendAsync("AgentConnected", agentId, agent.FullName);
                }
            }
            else
            {
                _logger.LogWarning("[LeadsHub] Agent {AgentId} connected but has no active session", agentId);
            }
        }
        else
        {
            _logger.LogWarning("[LeadsHub] Client {ConnectionId} connected without valid AgentId claim", connectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("[LeadsHub] Client disconnected: {ConnectionId}", connectionId);

        // Get AgentId from connection mapping
        if (_agentConnections.TryRemove(connectionId, out var agentId))
        {
            // Check if this was the last connection for this agent
            var hasOtherConnections = _agentConnections.Values.Any(id => id == agentId);

            if (!hasOtherConnections)
            {
                // This was the last connection - end session
                var activeSession = await _db.AgentSessions
                    .Where(s => s.AgentId == agentId && s.IsActive && s.ConnectionId == connectionId)
                    .FirstOrDefaultAsync();

                if (activeSession != null)
                {
                    // Note: Don't automatically set IsActive = false on disconnect
                    // The session cleanup service will handle stale sessions based on LastActivityTime
                    // This allows agents to reconnect without losing their session
                    _logger.LogInformation("[LeadsHub] Agent {AgentId} disconnected (ConnectionId: {ConnectionId})",
                        agentId, connectionId);

                    // Broadcast that agent disconnected
                    await Clients.All.SendAsync("AgentDisconnected", agentId);
                }
            }
            else
            {
                _logger.LogInformation("[LeadsHub] Agent {AgentId} still has {Count} other connection(s)",
                    agentId, _agentConnections.Values.Count(id => id == agentId));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
