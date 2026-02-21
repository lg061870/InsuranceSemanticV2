using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Adaptive card for checking insurance applicability.
/// </summary>
public class ApplicabilityCard
{
    public AdaptiveCardModel Create()
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "🎯 Insurance Applicability",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Is this life insurance applicable to someone like you (e.g., age, family, goals)?",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.ChoiceSet",
                Id = "applicable",
                Style = "expanded",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Yes, continue", Value = "yes" },
                    new CardChoice { Title = "No, not for me", Value = "no" }
                }
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