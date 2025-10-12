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
        public AdaptiveCardModel Create(bool isCaliforniaResident = false, string zipCode = "")
        {
            var bodyElements = new List<CardElement>
            {
                // Header - customized based on residency
                new CardElement
                {
                    Type = "TextBlock",
                    Text = isCaliforniaResident 
                        ? "📜 California Compliance & Consent" 
                        : "📜 Consent & Privacy Notice",
                    Weight = "Bolder",
                    Size = "Medium",
                    Color = "Dark"
                }
            };

            // Show CA-specific information if applicable
            if (isCaliforniaResident)
            {
                bodyElements.AddRange(new[]
                {
                    new CardElement
                    {
                        Type = "TextBlock",
                        Text = $"📍 California Resident (ZIP: {zipCode})",
                        Weight = "Bolder",
                        Color = "Accent",
                        Wrap = true
                    },
                    new CardElement
                    {
                        Type = "TextBlock",
                        Text = "As a California resident, you have enhanced privacy rights under the California Consumer Privacy Act (CCPA). The following disclosures are legally required.",
                        Wrap = true,
                        IsSubtle = true
                    }
                });
            }

            bodyElements.AddRange(new[]
            {
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
                }
            });

            // CCPA Notice Section - messaging varies by residency
            var ccpaTitle = isCaliforniaResident 
                ? "🔒 CCPA Notice (REQUIRED for CA Residents)"
                : "🔒 CCPA Notice (for California Residents)";
            
            var ccpaText = isCaliforniaResident
                ? "REQUIRED: As a California resident, you have privacy rights regarding your personal data collection, use, and sharing. We don't sell your information. You have the right to know, delete, and opt-out. Please acknowledge that you understand these rights."
                : "California residents: You have privacy rights regarding your personal data. We don't sell your information. Contact us for details or to opt out.";

            bodyElements.AddRange(new[]
            {
                new CardElement
                {
                    Type = "TextBlock",
                    Text = ccpaTitle,
                    Weight = "Bolder",
                    Color = isCaliforniaResident ? "Attention" : "Default",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = ccpaText,
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
            });

            // Legal disclaimer
            var disclaimer = isCaliforniaResident
                ? "⚖️ Legal Notice: California residents must receive privacy disclosures regardless of acknowledgment selection. Your responses affect available services and contact methods."
                : "ℹ️ Note: Your responses determine available services and contact methods. Privacy protections apply as requested.";

            bodyElements.Add(new CardElement
            {
                Type = "TextBlock",
                Text = disclaimer,
                Wrap = true,
                IsSubtle = true,
                Size = "Small"
            });

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