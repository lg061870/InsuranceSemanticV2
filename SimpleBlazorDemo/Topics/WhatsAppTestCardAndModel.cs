using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ConversaCore.Cards;
using ConversaCore.Models;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Simple adaptive card to collect a WhatsApp recipient phone number and optional test message.
/// </summary>
public class WhatsAppTestCard
{
    public AdaptiveCardModel Create(string? phoneNumber = "", string? customMessage = "")
    {
        var body = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "📱 WhatsApp Test",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Enter the WhatsApp phone number to send a test message to.",
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
                Text = "Optional test message text (leave blank to use the default message).",
                Wrap = true,
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "custom_message",
                IsMultiline = true,
                Placeholder = "Type a custom WhatsApp message...",
                Value = customMessage ?? string.Empty
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
                new CardAction { Type = "Action.Submit", Title = "Send test" }
            }
        };
    }
}

/// <summary>
/// Model bound to WhatsAppTestCard values.
/// </summary>
public class WhatsAppTestModel : BaseCardModel
{
    [Required]
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("custom_message")]
    public string? CustomMessage { get; set; }
}
