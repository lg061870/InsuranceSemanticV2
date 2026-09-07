using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Models;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Integrations.Zapier;

/// <summary>
/// Activity that triggers a Zapier webhook and optionally waits for response
/// </summary>
public class ZapierWebhookActivity : TopicFlowActivity
{
    private readonly IIntegrationService _integrationService;
    private readonly ILogger<ZapierWebhookActivity> _activityLogger;

    /// <summary>
    /// Webhook URL to trigger (customer's Zapier webhook URL)
    /// Can be set directly or retrieved from context
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Context key containing the webhook URL (alternative to WebhookUrl property)
    /// </summary>
    public string? WebhookUrlContextKey { get; set; }

    /// <summary>
    /// Context key containing the data to send
    /// </summary>
    public string DataContextKey { get; set; } = "zapier_data";

    /// <summary>
    /// Context key to store the response
    /// </summary>
    public string ResponseContextKey { get; set; } = "zapier_response";

    /// <summary>
    /// Optional event type identifier
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Whether to wait for response (default: false, fire-and-forget)
    /// </summary>
    public bool WaitForResponse { get; set; } = false;

    public ZapierWebhookActivity(
        string id,
        IIntegrationService integrationService,
        ILogger<ZapierWebhookActivity> logger) : base(id, logger)
    {
        _integrationService = integrationService;
        _activityLogger = logger;
    }

    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        TransitionTo(ActivityState.Running, input);

        try
        {
            // Get webhook URL
            var webhookUrl = WebhookUrl;
            if (string.IsNullOrEmpty(webhookUrl) && !string.IsNullOrEmpty(WebhookUrlContextKey))
            {
                webhookUrl = context.GetValue<string>(WebhookUrlContextKey);
            }

            if (string.IsNullOrEmpty(webhookUrl))
            {
                TransitionTo(ActivityState.Failed, "Webhook URL not provided");
                return ActivityResult.Cancelled("Webhook URL must be provided either directly or via WebhookUrlContextKey");
            }

            // Get data to send
            var data = context.GetValue<object>(DataContextKey);
            if (data == null)
            {
                _activityLogger?.LogWarning("No data found at context key {Key}, sending empty object", DataContextKey);
                data = new { };
            }

            // Build request
            var zapierRequest = new ZapierWebhookRequest
            {
                Data = data,
                EventType = EventType,
                Timestamp = DateTime.UtcNow
            };

            _activityLogger?.LogInformation(
                "Triggering Zapier webhook: {Url} with event type: {EventType}",
                MaskUrl(webhookUrl),
                EventType ?? "none");

            // Create integration request using the explicit webhook URL
            var integrationRequest = new IntegrationRequest<ZapierWebhookRequest>
            {
                Payload = zapierRequest,
                Url = webhookUrl,
                TimeoutSeconds = WaitForResponse ? 60 : 10,
                RetryCount = WaitForResponse ? 3 : 1
            };

            if (WaitForResponse)
            {
                    // Execute and wait for response
                    var response = await _integrationService.ExecuteAsync<ZapierWebhookRequest, ZapierWebhookResponse>(
                        "Zapier",
                        integrationRequest,
                        cancellationToken);

                    if (response.Success)
                    {
                        // Store both the typed data and the raw JSON so callers
                        // (like TestZapierTopic) can inspect the full payload.
                        context.SetValue(ResponseContextKey, response.Data);
                        if (!string.IsNullOrWhiteSpace(response.RawBody))
                        {
                            context.SetValue($"{ResponseContextKey}_raw", response.RawBody);
                        }
                        _activityLogger?.LogInformation("Zapier webhook completed with response in {Duration}ms", 
                            response.DurationMs);
                        
                        TransitionTo(ActivityState.Completed, response.Data);
                        return ActivityResult.Continue((object?)response.Data ?? new { });
                    }
                    else
                    {
                        _activityLogger?.LogWarning("Zapier webhook failed: {Error}", response.ErrorMessage);
                        context.SetValue($"{ResponseContextKey}_error", response.ErrorMessage);
                        
                        TransitionTo(ActivityState.Failed, response.ErrorMessage);
                        return ActivityResult.Continue(new { error = response.ErrorMessage });
                    }
            }
            else
            {
                // Framework lifecycle work is always awaited so cancellation and failure reach the runner.
                await _integrationService.ExecuteAsync<ZapierWebhookRequest, ZapierWebhookResponse>(
                    "Zapier", integrationRequest, cancellationToken);
                _activityLogger?.LogInformation("Zapier webhook triggered and completed");
                
                TransitionTo(ActivityState.Completed, "Webhook triggered");
                return ActivityResult.Continue(new { status = "triggered" });
            }
        }
        catch (OperationCanceledException)
        {
            TransitionTo(ActivityState.Failed, "Cancelled");
            return ActivityResult.Cancelled("Zapier webhook activity was cancelled");
        }
        catch (Exception ex)
        {
            _activityLogger?.LogError(ex, "Error executing Zapier webhook activity");
            TransitionTo(ActivityState.Failed, ex);
            throw;
        }
    }

    private static string MaskUrl(string url)
    {
        // Mask sensitive parts of webhook URL for logging
        var uri = new Uri(url);
        return $"{uri.Scheme}://{uri.Host}/hooks/catch/***/***/";
    }
}
