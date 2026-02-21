using System.Collections.Generic;
using ConversaCore.Cards;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Simple adaptive card for InitialpresentationTopic to capture the visitor's name
/// (and optionally ZIP code) with a clear explanation of why it's requested.
/// </summary>
public class FirstTimerContactCard
{
    public AdaptiveCardModel Create(string? name = "", string? zip = "")
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "Before we move on, please provide your name.",
                Weight = "Bolder",
                Size = "Medium",
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "We use your name just so we can address you properly. In a real quote experience, we may also ask for your ZIP code to see which plans and pricing are available in your area.",
                Wrap = true,
                IsSubtle = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "name",
                Placeholder = "Your name",
                Value = name ?? string.Empty
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "zip",
                Placeholder = "ZIP code (optional — helps match plans in your area)",
                Value = zip ?? string.Empty
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "Continue",
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
