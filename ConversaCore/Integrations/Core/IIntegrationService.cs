using ConversaCore.Integrations.Models;

namespace ConversaCore.Integrations.Core;

/// <summary>
/// Core service for executing external integrations
/// </summary>
public interface IIntegrationService
{
    /// <summary>
    /// Execute an integration call
    /// </summary>
    /// <typeparam name="TRequest">Request payload type</typeparam>
    /// <typeparam name="TResponse">Response payload type</typeparam>
    /// <param name="integrationName">Name of the integration (e.g., "Zapier", "DocuSign")</param>
    /// <param name="request">Request payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Integration response</returns>
    Task<IntegrationResponse<TResponse>> ExecuteAsync<TRequest, TResponse>(
        string integrationName,
        IntegrationRequest<TRequest> request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an integration is enabled and configured
    /// </summary>
    /// <param name="integrationName">Name of the integration</param>
    /// <returns>True if enabled and configured</returns>
    bool IsEnabled(string integrationName);

    /// <summary>
    /// Get list of all configured integrations
    /// </summary>
    IEnumerable<string> GetConfiguredIntegrations();
}
