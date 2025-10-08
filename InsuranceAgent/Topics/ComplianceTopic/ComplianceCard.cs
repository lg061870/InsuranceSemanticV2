using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Adaptive card for collecting TCPA consent and CCPA compliance acknowledgment.
    /// Ported from Copilot Studio JSON.
    /// </summary>
    public class ComplianceCard
    {
        public AdaptiveCardModel Create()
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📜 Consent & Compliance",
                    Weight = "Bolder",
                    Size = "Medium",
                    Color = "Dark"
                },

                // TCPA Consent Section
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📞 TCPA Consent",
                    Weight = "Bolder",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "By submitting this form, you agree to be contacted by a licensed insurance agent or partner at the phone number and email provided, including via automated dialer, pre-recorded messages, or text messages, even if your number is on a Do Not Call list. Your consent is not a condition of purchase. Message and data rates may apply.",
                    Wrap = true,
                    IsSubtle = true
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "tcpac_consent",
                    Text = "I agree to be contacted as described above",
                    Value = "no"
                },

                // CCPA Notice Section
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🔒 CCPA Notice (for California Residents)",
                    Weight = "Bolder",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "If you are a California resident, you have the right to know how your personal data is used. We do not sell your personal information. You may request more details or opt out at any time.",
                    Wrap = true,
                    IsSubtle = true
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "ccpa_acknowledged",
                    Text = "I acknowledge the privacy notice",
                    Value = "no"
                }
            };

            var actions = new List<CardAction>
            {
                new CardAction
                {
                    Type = "Action.Submit",
                    Title = "✅ Submit"
                }
            };

            return new AdaptiveCardModel
            {
                Type = "AdaptiveCard",
                Schema = "https://adaptivecards.io/schemas/adaptive-card.json",
                Version = "1.5",
                Body = bodyElements,
                Actions = actions
            };
        }
    }
}