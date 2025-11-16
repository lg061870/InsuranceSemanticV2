using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics;

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

            // Employment Status Options
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "employment_status",
                Value = employmentStatus ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Full-Time", Value = "Full-Time" },
                    new CardChoice { Title = "Part-Time", Value = "Part-Time" },
                    new CardChoice { Title = "Self-Employed", Value = "Self-Employed" },
                    new CardChoice { Title = "Unemployed", Value = "Unemployed" },
                    new CardChoice { Title = "Retired", Value = "Retired" },
                    new CardChoice { Title = "Student", Value = "Student" }
                }
            },

            // Household Income Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "💰 What is your total household income?",
                Wrap = true
            },

            // Income Options
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "household_income",
                Value = householdIncomeBand ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Under $25k", Value = "Under $25k" },
                    new CardChoice { Title = "$25k–$50k", Value = "$25k–$50k" },
                    new CardChoice { Title = "$50k–$75k", Value = "$50k–$75k" },
                    new CardChoice { Title = "$75k–$100k", Value = "$75k–$100k" },
                    new CardChoice { Title = "Over $100k", Value = "Over $100k" },
                    new CardChoice { Title = "Prefer not to say", Value = "Prefer not to say" }
                }
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