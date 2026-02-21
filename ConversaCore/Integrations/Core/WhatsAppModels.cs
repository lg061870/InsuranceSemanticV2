using System.Text.Json.Serialization;

namespace ConversaCore.Integrations.Core;

/// <summary>
/// Minimal request model for sending messages via the WhatsApp Business Cloud API.
/// This is intentionally simple and focuses on text and template messages.
/// </summary>
public class WhatsAppSendRequest
{
    /// <summary>
    /// Recipient phone number in E.164 format (e.g. +15555550123).
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Message type: "text" for free-form within CSW, or "template".
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// Text body when Type == "text".
    /// </summary>
    public string? BodyText { get; set; }

    /// <summary>
    /// Template name when Type == "template" (e.g. "order_update").
    /// </summary>
    public string? TemplateName { get; set; }

    /// <summary>
    /// Template language code (e.g. "en_US").
    /// </summary>
    public string? TemplateLanguage { get; set; }

    /// <summary>
    /// Optional template parameters (basic text parameters only for now).
    /// </summary>
    public List<string>? TemplateParameters { get; set; }

    /// <summary>
    /// Optional tenant identifier to help resolve per-tenant configuration.
    /// </summary>
    public string? TenantId { get; set; }
}

/// <summary>
/// Minimal response model capturing WhatsApp message id and status.
/// </summary>
public class WhatsAppSendResponse
{
    public string? MessageId { get; set; }

    public string? Status { get; set; }

    /// <summary>
    /// Raw response JSON for debugging/inspection.
    /// </summary>
    public string? Raw { get; set; }
}
