using ConversaCore.Cards;
using System.Collections.Generic;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Adaptive card for TCPA consent.
/// </summary>
public class TcpaConsentCard
{
    public AdaptiveCardModel Create()
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "📜 TCPA Consent",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "By checking this box, I consent to receive marketing communications via phone and email as permitted by TCPA regulations.",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "tcpaConsent",
                Text = "I consent to TCPA marketing communications",
                Value = "false" // Default to false
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