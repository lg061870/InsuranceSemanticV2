using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.EmploymentTopic
{
    /// <summary>
    /// Adaptive card for collecting employment information.
    /// Ported from Copilot Studio JSON.
    /// </summary>
    public class EmploymentCard
    {
        public AdaptiveCardModel Create(
            string? employmentStatus = "",
            string? householdIncomeBand = "",
            string? occupation = "")
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💼 Employment Information",
                    Weight = "Bolder",
                    Size = "Medium",
                    Color = "Dark"
                },

                // Employment Status Question
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📌 What is your current employment status?",
                    Wrap = true
                },

                // Employment Status Options - Row 1
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "employment_fulltime",
                    Text = "Full-Time",
                    Value = employmentStatus == "Full-Time" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "employment_parttime",
                    Text = "Part-Time",
                    Value = employmentStatus == "Part-Time" ? "true" : "false"
                },

                // Employment Status Options - Row 2
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "employment_self",
                    Text = "Self-Employed",
                    Value = employmentStatus == "Self-Employed" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "employment_unemployed",
                    Text = "Unemployed",
                    Value = employmentStatus == "Unemployed" ? "true" : "false"
                },

                // Employment Status Options - Row 3
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "employment_retired",
                    Text = "Retired",
                    Value = employmentStatus == "Retired" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "employment_student",
                    Text = "Student",
                    Value = employmentStatus == "Student" ? "true" : "false"
                },

                // Household Income Question
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💰 What is your total household income?",
                    Wrap = true
                },

                // Income Options - Row 1
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "income_under_25",
                    Text = "Under $25k",
                    Value = householdIncomeBand == "Under $25k" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "income_25_50",
                    Text = "$25k–$50k",
                    Value = householdIncomeBand == "$25k–$50k" ? "true" : "false"
                },

                // Income Options - Row 2
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "income_50_75",
                    Text = "$50k–$75k",
                    Value = householdIncomeBand == "$50k–$75k" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "income_75_100",
                    Text = "$75k–$100k",
                    Value = householdIncomeBand == "$75k–$100k" ? "true" : "false"
                },

                // Income Options - Row 3
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "income_over_100",
                    Text = "Over $100k",
                    Value = householdIncomeBand == "Over $100k" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "income_no_say",
                    Text = "Prefer not to say",
                    Value = householdIncomeBand == "Prefer not to say" ? "true" : "false"
                },

                // Occupation Question
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🧦 What is your occupation? (Optional)",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "occupation",
                    Text = "Enter your occupation",
                    Value = occupation ?? ""
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