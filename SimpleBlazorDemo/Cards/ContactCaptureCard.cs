using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Adaptive card for capturing contact information.
/// </summary>
public class ContactCaptureCard
{
    public AdaptiveCardModel Create(
        string? name = "",
        string? email = "",
        string? phone = "")
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "📞 Contact Information",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "name",
                Placeholder = "Full Name",
                Value = name ?? ""
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "email",
                Placeholder = "Email Address",
                Value = email ?? ""
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "phone",
                Placeholder = "Phone Number",
                Value = phone ?? ""
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "Submit",
                Data = new { action = "submit" }
            }
        };

        return new AdaptiveCardModel
        {
            Body = bodyElements,
            Actions = actions
        };
    }
}