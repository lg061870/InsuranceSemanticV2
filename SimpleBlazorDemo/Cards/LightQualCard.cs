using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Adaptive card for light qualification questions.
/// </summary>
public class LightQualCard
{
    public AdaptiveCardModel Create(
        string? goals = "",
        string? family = "",
        string? mortgage = "")
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "🔍 Light Qualification",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "goals",
                Placeholder = "Your main goals (e.g., protect family)",
                Value = goals ?? ""
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "family",
                Placeholder = "Family situation (e.g., spouse, kids)",
                Value = family ?? ""
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "mortgage",
                Placeholder = "Mortgage or debts",
                Value = mortgage ?? ""
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