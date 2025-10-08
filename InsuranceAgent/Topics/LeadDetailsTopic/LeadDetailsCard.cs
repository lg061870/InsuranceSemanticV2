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
            string? leadIntent = "",
            string? appointmentDateTime = "",
            bool? followUpNeeded = null,
            string? notesForSalesAgent = "",
            string? leadUrl = "")
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
                    Text = "🧑 Lead Name",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "lead_name",
                    Text = "Full name",
                    Value = leadName ?? ""
                },

                // Preferred Language
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🗣️ Preferred Language",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "language",
                    Text = "e.g., English, Spanish",
                    Value = language ?? ""
                },

                // Lead Source
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🌐 Lead Source",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "lead_source",
                    Text = "e.g., Website, Referral",
                    Value = leadSource ?? ""
                },

                // Interest Level
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📈 Interest Level",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "interest_level",
                    Text = "e.g., High, Medium, Low",
                    Value = interestLevel ?? ""
                },

                // Lead Intent
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🎯 Lead Intent",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "lead_intent",
                    Text = "e.g., Learn, Buy, Compare",
                    Value = leadIntent ?? ""
                },

                // Appointment Date/Time
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📅 Appointment Date/Time",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "appointment_date_time",
                    Text = "e.g., 2025-09-05 14:30",
                    Value = appointmentDateTime ?? ""
                },

                // Needs Follow-Up
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🔁 Needs Follow-Up?",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "follow_up_needed",
                    Text = "Yes",
                    Value = followUpNeeded == true ? "true" : "false"
                },

                // Notes for Sales Agent
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📝 Notes for Sales Agent",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "notes_for_sales_agent",
                    Text = "Optional notes",
                    Value = notesForSalesAgent ?? ""
                },

                // Lead URL
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🔗 Lead URL (optional)",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "lead_url",
                    Text = "e.g., https://crm.example.com/lead",
                    Value = leadUrl ?? ""
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