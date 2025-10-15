using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.LeadDetailsTopic
{
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
                    Type = "Input.Text",
                    Id = "language",
                    Text = "English, Spanish, French, etc.",
                    Value = language ?? ""
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
                    Type = "Input.Text",
                    Id = "lead_source",
                    Text = "Website, Referral, Google Ads, etc.",
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
                    Type = "Input.Text",
                    Id = "interest_level",
                    Text = "High, Medium, or Low",
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
                    Type = "Input.Text",
                    Id = "lead_intent",
                    Text = "Buy, Compare, Learn, or Schedule",
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
}