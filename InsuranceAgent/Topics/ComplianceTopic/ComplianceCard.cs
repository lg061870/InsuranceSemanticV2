using ConversaCore.Cards;
using System.Collections.Generic;

namespace InsuranceAgent.Topics; 
/// <summary>
/// Adaptive card for collecting TCPA consent and ZIP code.
/// Detects California residency by ZIP range (90001–96162).
/// </summary>
public class ComplianceCard {
    public AdaptiveCardModel Create(string? zipCode = "") {
        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "📜 Communication Consent & ZIP Verification",
                Weight = "Bolder",
                Size = "Medium",
                Color = "Dark"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "Before continuing, please provide your consent to be contacted and your ZIP code for compliance purposes.",
                Wrap = true,
                IsSubtle = true
            },

            // TCPA Consent
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
                Id = "tcpa_consent",
                Style = "compact",
                Value = "prefer_not_to_answer",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Don't want to answer", Value = "prefer_not_to_answer" },
                    new CardChoice { Title = "Yes, I agree", Value = "yes" },
                    new CardChoice { Title = "No, I do not agree", Value = "no" }
                }
            },

            // ZIP Code Field
            new CardElement
            {
                Type = "TextBlock",
                Text = "📍 ZIP Code (5 digits)",
                Weight = "Bolder",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "zip_code",
                Placeholder = "e.g., 94107",
                Value = zipCode ?? "",
                IsRequired = true,
                Regex = @"^\d{5}$"
            },

            // Disclaimer
            new CardElement
            {
                Type = "TextBlock",
                Text = "⚖️ Your ZIP Code helps us apply the right privacy protections. California residents receive additional disclosures.",
                Wrap = true,
                IsSubtle = true,
                Size = "Small"
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "✅ Submit",
                Data = new { action = "submitCompliance" }
            }
        };

        return new AdaptiveCardModel {
            Type = "AdaptiveCard",
            Schema = "https://adaptivecards.io/schemas/adaptive-card.json",
            Version = "1.5",
            Body = bodyElements,
            Actions = actions
        };
    }
}
