using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics;

/// <summary>
/// Adaptive card for collecting lead management and sales tracking information.
/// Ported from Copilot Studio JSON.
/// </summary>
public class LeadDetailsCard
{
    public AdaptiveCardModel Create(
        string? leadName = "",
        string? language = "",
        string? leadSource = "",
        string? interestLevel = "",
        string? leadIntent = "")
    {
        var bodyElements = new List<CardElement>
        {
            // Header
            new CardElement
            {
                Type = "TextBlock",
                Text = "📇 Lead Details",
                Weight = "Bolder",
                Size = "Medium"
            },

            // Lead Name
            new CardElement
            {
                Type = "TextBlock",
                Text = "🧑 Lead Name ⓘ",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "lead_name",
                Text = "Enter prospect's full name",
                Value = leadName ?? ""
            },

            // Preferred Language
            new CardElement
            {
                Type = "TextBlock",
                Text = "🗣️ Preferred Language ⓘ",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "language",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "English", Value = "English" },
                    new CardChoice { Title = "Spanish", Value = "Spanish" },
                    new CardChoice { Title = "French", Value = "French" },
                    new CardChoice { Title = "German", Value = "German" },
                    new CardChoice { Title = "Chinese", Value = "Chinese" }
                },
                Value = language ?? "",
                AllowCustom = true,
                CustomPlaceholder = "Or enter other language..."
            },

            // Lead Source
            new CardElement
            {
                Type = "TextBlock",
                Text = "🌐 Lead Source ⓘ",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "lead_source",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Website", Value = "Website" },
                    new CardChoice { Title = "Referral", Value = "Referral" },
                    new CardChoice { Title = "Google Ads", Value = "Google Ads" },
                    new CardChoice { Title = "Social Media", Value = "Social Media" },
                    new CardChoice { Title = "Email", Value = "Email" }
                },
                Value = leadSource ?? ""
            },

            // Interest Level
            new CardElement
            {
                Type = "TextBlock",
                Text = "📈 Interest Level ⓘ",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "interest_level",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "High", Value = "High" },
                    new CardChoice { Title = "Medium", Value = "Medium" },
                    new CardChoice { Title = "Low", Value = "Low" }
                },
                Value = interestLevel ?? ""
            },

            // Lead Intent
            new CardElement
            {
                Type = "TextBlock",
                Text = "🎯 Lead Intent ⓘ",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "lead_intent",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Buy", Value = "Buy" },
                    new CardChoice { Title = "Compare", Value = "Compare" },
                    new CardChoice { Title = "Learn", Value = "Learn" },
                    new CardChoice { Title = "Schedule", Value = "Schedule" }
                },
                Value = leadIntent ?? ""
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "➡️ Next"
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