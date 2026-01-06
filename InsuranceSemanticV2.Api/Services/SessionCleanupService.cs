using InsuranceSemanticV2.Data.DataContext;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Services;

/// <summary>
/// Background service that periodically cleans up stale agent sessions
/// Runs every 5 minutes to check for sessions that haven't sent a heartbeat
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public SessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SessionCleanupService] Starting session cleanup background service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleSessions(stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogInformation("[SessionCleanupService] Session cleanup service is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SessionCleanupService] Error during session cleanup");
                // Continue running despite errors
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
    }

    private async Task CleanupStaleSessions(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Get timeout values from configuration
        var awayTimeoutMinutes = configuration.GetValue<int>("Session:AwayTimeoutMinutes", 5);
        var offlineTimeoutMinutes = configuration.GetValue<int>("Session:OfflineTimeoutMinutes", 15);

        var now = DateTime.UtcNow;
        var awayThreshold = now.AddMinutes(-awayTimeoutMinutes);
        var offlineThreshold = now.AddMinutes(-offlineTimeoutMinutes);

        // Find sessions that need status updates
        var activeSessions = await dbContext.AgentSessions
            .Where(s => s.IsActive)
            .ToListAsync(stoppingToken);

        var sessionsUpdated = 0;
        var sessionsEnded = 0;

        foreach (var session in activeSessions)
        {
            // If no activity for 15+ minutes, end the session
            if (session.LastActivityTime < offlineThreshold)
            {
                session.IsActive = false;
                session.Status = "Offline";
                session.LogoutTime = now;
                sessionsEnded++;

                _logger.LogInformation(
                    "[SessionCleanupService] Ended stale session {SessionId} for Agent {AgentId} (last activity: {LastActivity})",
                    session.AgentSessionId, session.AgentId, session.LastActivityTime);
            }
            // If no activity for 5+ minutes, mark as Away
            else if (session.LastActivityTime < awayThreshold && session.Status != "Away")
            {
                session.Status = "Away";
                sessionsUpdated++;

                _logger.LogInformation(
                    "[SessionCleanupService] Set session {SessionId} for Agent {AgentId} to 'Away' (last activity: {LastActivity})",
                    session.AgentSessionId, session.AgentId, session.LastActivityTime);
            }
        }

        if (sessionsUpdated > 0 || sessionsEnded > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation(
                "[SessionCleanupService] Cleanup complete: {UpdatedCount} sessions set to Away, {EndedCount} sessions ended",
                sessionsUpdated, sessionsEnded);
        }
        else
        {
            _logger.LogDebug("[SessionCleanupService] No stale sessions found");
        }
    }
}
