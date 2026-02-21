using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ConversaCore.Cards;
using ConversaCore.Models;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Adaptive card used by TestZapierTopic to collect a phone number
/// and a free-form message that will be sent to Zapier.
/// </summary>
public class ZapierTestCard
{
    public AdaptiveCardModel Create(string? phoneNumber = "", string? message = "")
    {
        var body = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "Zapier Webhook Test",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Enter the phone number and a test message that will be sent to your Zapier webhook.",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "phone_number",
                Placeholder = "+15555550123 (E.164 format)",
                Value = phoneNumber ?? string.Empty
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Message text to include in the Zap payload.",
                Wrap = true,
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "message",
                IsMultiline = true,
                Placeholder = "Type a test message...",
                Value = message ?? string.Empty
            }
        };

        return new AdaptiveCardModel
        {
            Type = "AdaptiveCard",
            Schema = "https://adaptivecards.io/schemas/adaptive-card.json",
            Version = "1.5",
            Body = body,
            Actions = new List<CardAction>
            {
                new CardAction { Type = "Action.Submit", Title = "Send to Zapier" }
            }
        };
    }
}

/// <summary>
/// Model bound to ZapierTestCard values.
/// </summary>
public class ZapierTestModel : BaseCardModel
{
    [Required]
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
