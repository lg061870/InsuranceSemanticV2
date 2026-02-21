using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Adaptive card for explaining insurance types.
/// </summary>
public class InsuranceTypesCard
{
    public AdaptiveCardModel Create()
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "📚 Insurance Types",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Term Life: Provides coverage for a specific period.",
                Wrap = true
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Whole Life: Lifetime coverage with cash value.",
                Wrap = true
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Universal Life: Flexible premiums and death benefits.",
                Wrap = true
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