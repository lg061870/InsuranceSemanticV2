using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics;

/// <summary>
/// Adaptive card for collecting contact information and preferences.
/// Ported from Copilot Studio JSON.
/// </summary>
public class ContactInfoCard
{
    public AdaptiveCardModel Create(
        string? fullName = "",
        string? phoneNumber = "",
        string? emailAddress = "",
        string? bestContactTime = "",
        string? contactMethod = "",
        bool consentContact = false)
    {
        var bodyElements = new List<CardElement>
        {
            // Header
            new CardElement
            {
                Type = "TextBlock",
                Text = "📇 Contact Information",
                Weight = "Bolder",
                Size = "Medium",
                Color = "Dark"
            },

            // Full Name
            new CardElement
            {
                Type = "TextBlock",
                Text = "✍️ Your Full Name",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "full_name",
                Text = "Enter your name",
                Value = fullName ?? ""
            },

            // Phone Number
            new CardElement
            {
                Type = "TextBlock",
                Text = "📞 Phone Number",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "phone_number",
                Text = "Enter your phone number",
                Style = "Tel",
                Value = phoneNumber ?? ""
            },

            // Email Address
            new CardElement
            {
                Type = "TextBlock",
                Text = "📧 Email Address",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "email_address",
                Text = "Enter your email",
                Style = "Email",
                Value = emailAddress ?? ""
            },

            // Best Time to Contact
            new CardElement
            {
                Type = "TextBlock",
                Text = "🕒 Best Time to Contact You",
                Wrap = true
            },

            // Contact Time Toggles - Row 1 (Morning, Afternoon)
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_time_morning",
                Text = "Morning",
                Value = bestContactTime == "Morning" ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_time_afternoon",
                Text = "Afternoon",
                Value = bestContactTime == "Afternoon" ? "true" : "false"
            },

            // Contact Time Toggles - Row 2 (Evening, Anytime)
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_time_evening",
                Text = "Evening",
                Value = bestContactTime == "Evening" ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_time_any",
                Text = "Anytime",
                Value = bestContactTime == "Anytime" ? "true" : "false"
            },

            // Preferred Contact Method
            new CardElement
            {
                Type = "TextBlock",
                Text = "📬 Preferred Contact Method",
                Wrap = true
            },

            // Contact Method Toggles
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_method_phone",
                Text = "Phone",
                Value = contactMethod == "Phone" ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_method_email",
                Text = "Email",
                Value = contactMethod == "Email" ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "contact_method_either",
                Text = "Either",
                Value = contactMethod == "Either" ? "true" : "false"
            },

            // Consent
            new CardElement
            {
                Type = "TextBlock",
                Text = "🔒 I agree to be contacted regarding insurance services.",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "consent_contact",
                Text = "I consent",
                Value = consentContact ? "yes" : "no"
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