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
                    Text = "I agree to be contacted by licensed insurance agents via phone, email, or text at the provided contact information, including automated messages. Consent not required for purchase. Standard rates apply.",
                    Wrap = true,
                    IsSubtle = true
                },
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "tcpac_consent",
                    Text = "I agree to be contacted as described above",
                    Style = "compact",
                    Value = "prefer_not_to_answer",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "Don't want to answer", Value = "prefer_not_to_answer" },
                        new CardChoice { Title = "Yes, I agree", Value = "yes" },
                        new CardChoice { Title = "No, I do not agree", Value = "no" }
                    }
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
                    Text = "California residents: You have privacy rights regarding your personal data. We don't sell your information. Contact us for details or to opt out.",
                    Wrap = true,
                    IsSubtle = true
                },
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "ccpa_acknowledged",
                    Text = "I acknowledge the privacy notice",
                    Style = "compact",
                    Value = "prefer_not_to_answer",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "Don't want to answer", Value = "prefer_not_to_answer" },
                        new CardChoice { Title = "Yes, I acknowledge", Value = "yes" },
                        new CardChoice { Title = "No, I do not acknowledge", Value = "no" }
                    }
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