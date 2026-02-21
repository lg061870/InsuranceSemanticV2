using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ConversaCore.Integrations.Exceptions;
using ConversaCore.Integrations.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConversaCore.Integrations.Core;

/// <summary>
/// Implementation of integration service with retry and basic error handling
/// </summary>
public class IntegrationService : IIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IntegrationService> _logger;
    private readonly IntegrationsConfiguration _config;

    public IntegrationService(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationsConfiguration> config,
        ILogger<IntegrationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<IntegrationResponse<TResponse>> ExecuteAsync<TRequest, TResponse>(
        string integrationName,
        IntegrationRequest<TRequest> request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryAttempts = 0;

        _logger.LogInformation(
            "Executing integration {IntegrationName} with payload type {PayloadType}",
            integrationName,
            typeof(TRequest).Name);

        if (!IsEnabled(integrationName))
        {
            throw new IntegrationException(
                integrationName,
                $"Integration '{integrationName}' is not enabled or configured");
        }

        var integrationConfig = _config.Integrations[integrationName];

        try
        {
            var httpClient = _httpClientFactory.CreateClient(integrationName);

            // Set timeout
            httpClient.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds);

            // Build URL with query parameters. If the request specifies an explicit URL,
            // prefer that; otherwise fall back to the integration's configured BaseUrl.
            var url = !string.IsNullOrWhiteSpace(request.Url)
                ? request.Url!
                : integrationConfig.BaseUrl ?? string.Empty;
            if (request.QueryParameters?.Any() == true)
            {
                var query = string.Join("&", request.QueryParameters.Select(kvp => 
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
                url = $"{url}?{query}";
            }

            // Execute with basic retry logic
            HttpResponseMessage? response = null;
            for (int attempt = 0; attempt <= request.RetryCount; attempt++)
            {
                try
                {
                    retryAttempts = attempt;
                    // IMPORTANT: HttpRequestMessage instances cannot be reused across SendAsync calls.
                    // Create a fresh request for each retry attempt.
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(request.Payload)
                    };

                    // Add headers per attempt
                    if (request.Headers != null)
                    {
                        foreach (var header in request.Headers)
                        {
                            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    response = await httpClient.SendAsync(httpRequest, cancellationToken);
                    
                    if (response.IsSuccessStatusCode || attempt == request.RetryCount)
                    {
                        break;
                    }
                    
                    // Wait before retry with exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
                catch (Exception) when (attempt < request.RetryCount)
                {
                    // Retry on exception unless it's the last attempt
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }

            if (response == null)
            {
                throw new IntegrationException(integrationName, "Failed to get response after retries");
            }

            stopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = string.IsNullOrWhiteSpace(responseContent)
                    ? default
                    : JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                _logger.LogInformation(
                    "Integration {IntegrationName} completed successfully in {Duration}ms",
                    integrationName,
                    stopwatch.ElapsedMilliseconds);

                return new IntegrationResponse<TResponse>
                {
                    Success = true,
                    Data = data,
                    StatusCode = (int)response.StatusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    RetryAttempts = retryAttempts,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    RawBody = responseContent
                };
            }
            else
            {
                _logger.LogWarning(
                    "Integration {IntegrationName} failed with status {StatusCode}: {Response}",
                    integrationName,
                    response.StatusCode,
                    responseContent);

                return new IntegrationResponse<TResponse>
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}",
                    StatusCode = (int)response.StatusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    RetryAttempts = retryAttempts,
                    RawBody = responseContent
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Integration {IntegrationName} threw exception after {Duration}ms",
                integrationName,
                stopwatch.ElapsedMilliseconds);

            return new IntegrationResponse<TResponse>
            {
                Success = false,
                ErrorMessage = ex.Message,
                StatusCode = 0,
                DurationMs = stopwatch.ElapsedMilliseconds,
                RetryAttempts = retryAttempts
            };
        }
    }

    public bool IsEnabled(string integrationName)
    {
        return _config.Integrations.TryGetValue(integrationName, out var config) && config.Enabled;
    }

    public IEnumerable<string> GetConfiguredIntegrations()
    {
        return _config.Integrations
            .Where(kvp => kvp.Value.Enabled)
            .Select(kvp => kvp.Key);
    }
}
