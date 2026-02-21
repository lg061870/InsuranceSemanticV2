using System.Collections.Generic;
using ConversaCore.Cards;

namespace SimpleBlazorDemo.Cards;

/// <summary>
/// Combined TCPA / CCPA consent card for the InitialpresentationTopic flow.
/// </summary>
public class FirstTimerConsentCard
{
    public AdaptiveCardModel Create()
    {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "☎️ Consent to contact & privacy",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "To respect your preferences and comply with regulations, we need your permission to contact you about insurance options.",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "tcpConsent",
                Text = "I agree that you may contact me about insurance quotes and options using the details I provide, as allowed under TCPA.",
                Value = "false"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "If you live in California, we also provide a brief CCPA privacy notice so you know how your information may be used.",
                Wrap = true,
                IsSubtle = true
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ccpaAcknowledged",
                Text = "I live in California or want to review the CCPA notice, and I acknowledge this privacy information.",
                Value = "false"
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "Confirm & continue",
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
