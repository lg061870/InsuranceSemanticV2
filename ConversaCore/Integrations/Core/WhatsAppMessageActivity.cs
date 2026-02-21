using System.Text.Json;
using System.Text.Json.Nodes;
using ConversaCore.Integrations.Models;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Integrations.Core;

/// <summary>
/// Activity that sends a simple WhatsApp message via the WhatsApp Business Cloud API
/// using the generic IntegrationService. For now this assumes a single developer
/// account configured under the "WhatsApp" integration, but the payload includes
/// an optional TenantId for future multi-tenant routing.
/// </summary>
public class WhatsAppMessageActivity : TopicFlowActivity
{
    private readonly IIntegrationService _integrationService;
    private readonly ILogger<WhatsAppMessageActivity> _activityLogger;

    /// <summary>
    /// Context key containing the recipient WhatsApp number (E.164), e.g. "+15555550123".
    /// </summary>
    public string PhoneNumberContextKey { get; set; } = "whatsapp_to";

    /// <summary>
    /// Context key containing the free-form text body to send when using text messages.
    /// </summary>
    public string MessageTextContextKey { get; set; } = "whatsapp_message_text";

    /// <summary>
    /// Optional context key for the template name (if sending a template message).
    /// When provided, the activity will attempt to send a template instead of free text.
    /// </summary>
    public string? TemplateNameContextKey { get; set; }

    /// <summary>
    /// Optional context key for template language (e.g. "en_US").
    /// </summary>
    public string? TemplateLanguageContextKey { get; set; }

    /// <summary>
    /// Optional context key for template parameters (List&lt;string&gt; or string[]).
    /// </summary>
    public string? TemplateParametersContextKey { get; set; }

    /// <summary>
    /// Optional context key that contains a tenant identifier used when resolving
    /// per-tenant WhatsApp credentials in the integration layer.
    /// </summary>
    public string? TenantIdContextKey { get; set; }

    /// <summary>
    /// Context key to store the integration response.
    /// </summary>
    public string ResponseContextKey { get; set; } = "whatsapp_response";

    public WhatsAppMessageActivity(
        string id,
        IIntegrationService integrationService,
        ILogger<WhatsAppMessageActivity> logger) : base(id, logger)
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
            var to = SafeGet<string>(context, PhoneNumberContextKey);
            if (string.IsNullOrWhiteSpace(to))
            {
                TransitionTo(ActivityState.Failed, $"Missing WhatsApp recipient at '{PhoneNumberContextKey}'");
                return ActivityResult.Cancelled($"WhatsApp recipient phone number not found in context key '{PhoneNumberContextKey}'.");
            }

            // Decide whether we're sending a template or a simple text message.
            var templateName = !string.IsNullOrWhiteSpace(TemplateNameContextKey)
                ? SafeGet<string>(context, TemplateNameContextKey)
                : null;

            var waRequest = new WhatsAppSendRequest
            {
                To = to,
            };

            if (!string.IsNullOrWhiteSpace(templateName))
            {
                waRequest.Type = "template";
                waRequest.TemplateName = templateName;

                if (!string.IsNullOrWhiteSpace(TemplateLanguageContextKey))
                {
                    waRequest.TemplateLanguage = SafeGet<string>(context, TemplateLanguageContextKey);
                }

                if (!string.IsNullOrWhiteSpace(TemplateParametersContextKey))
                {
                    var rawParams = context.GetValue<object>(TemplateParametersContextKey);
                    switch (rawParams)
                    {
                        case List<string> list:
                            waRequest.TemplateParameters = list;
                            break;
                        case string[] arr:
                            waRequest.TemplateParameters = arr.ToList();
                            break;
                        case IEnumerable<string> enumerable:
                            waRequest.TemplateParameters = enumerable.ToList();
                            break;
                    }
                }
            }
            else
            {
                waRequest.Type = "text";
                waRequest.BodyText = SafeGet<string>(context, MessageTextContextKey) ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(TenantIdContextKey))
            {
                waRequest.TenantId = SafeGet<string>(context, TenantIdContextKey);
            }

            _activityLogger?.LogInformation(
                "Sending WhatsApp {Type} message to {To} (Tenant={Tenant})",
                waRequest.Type,
                MaskPhoneNumber(to),
                waRequest.TenantId ?? "default");

            var integrationRequest = new IntegrationRequest<WhatsAppSendRequest>
            {
                Payload = waRequest,
                TimeoutSeconds = 30,
                RetryCount = 2
            };

            var response = await _integrationService.ExecuteAsync<WhatsAppSendRequest, WhatsAppSendResponse>(
                "WhatsApp",
                integrationRequest,
                cancellationToken);

            if (response.Success)
            {
                // Store both typed response and raw JSON (if populated by integration layer).
                context.SetValue(ResponseContextKey, response.Data);
                if (!string.IsNullOrWhiteSpace(response.RawBody))
                {
                    context.SetValue($"{ResponseContextKey}_raw", response.RawBody);
                }

                _activityLogger?.LogInformation(
                    "WhatsApp message sent successfully (MessageId={MessageId}, Duration={Duration}ms)",
                    response.Data?.MessageId,
                    response.DurationMs);

                TransitionTo(ActivityState.Completed, response.Data);
                return ActivityResult.Continue((object?)(response.Data ?? new WhatsAppSendResponse { Status = "sent" }));
            }

            _activityLogger?.LogWarning(
                "WhatsApp message failed: {Error}",
                response.ErrorMessage);

            context.SetValue($"{ResponseContextKey}_error", response.ErrorMessage ?? "Unknown error");
            TransitionTo(ActivityState.Failed, response.ErrorMessage ?? "Unknown error");
            return ActivityResult.Continue(new { error = response.ErrorMessage ?? "WhatsApp send failed" });
        }
        catch (OperationCanceledException)
        {
            TransitionTo(ActivityState.Failed, "Cancelled");
            return ActivityResult.Cancelled("WhatsApp message activity was cancelled");
        }
        catch (Exception ex)
        {
            _activityLogger?.LogError(ex, "Error executing WhatsApp message activity");
            TransitionTo(ActivityState.Failed, ex);
            throw;
        }
    }

    private static T? SafeGet<T>(TopicWorkflowContext ctx, string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return default;

        try
        {
            return ctx.GetValue<T>(key);
        }
        catch
        {
            return default;
        }
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4) return phone;
        var visible = phone[^4..];
        return new string('*', phone.Length - 4) + visible;
    }
}
