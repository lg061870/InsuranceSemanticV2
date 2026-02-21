using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Adaptive card for CCPA notice.
/// </summary>
public class CcpaNoticeCard
{
    public AdaptiveCardModel Create()
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "🔒 CCPA Notice",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "We collect personal information for insurance lead qualification. You have rights under CCPA including access, deletion, and opt-out.",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ccpaAcknowledged",
                Text = "I acknowledge the CCPA notice",
                Value = "false"
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "Acknowledge",
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