using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Simple adaptive card for displaying trust signals.
/// </summary>
public class TrustSignalsCard
{
    public AdaptiveCardModel Create()
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "🛡️ Trust Signals",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "We are licensed by CA Dept of Insurance.",
                Wrap = true
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Rated 4.8/5 on Google (500 reviews).",
                Wrap = true
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Partners: MetLife, Prudential.",
                Wrap = true
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "FINRA-registered advisors.",
                Wrap = true
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "Continue",
                Data = new { action = "trust_signals_continue" }
            }
        };

        return new AdaptiveCardModel
        {
            Body = bodyElements,
            Actions = actions
        };
    }
}