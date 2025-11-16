using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics;

/// <summary>
/// Adaptive card for collecting coverage intent and preferences.
/// Ported from Copilot Studio JSON.
/// </summary>
public class CoverageIntentCard
{
    public AdaptiveCardModel Create(
        List<string>? selectedCoverageTypes = null,
        string? coverageStartTime = "",
        string? desiredCoverageAmount = "")
    {
        selectedCoverageTypes ??= new List<string>();

        var bodyElements = new List<CardElement>
        {
            // Header
            new CardElement
            {
                Type = "TextBlock",
                Text = "🎯 Coverage Intent",
                Weight = "Bolder",
                Size = "Medium",
                Color = "Dark"
            },

            // Coverage Type Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "🛡️ What type of coverage are you interested in?",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "coverage_type",
                Value = selectedCoverageTypes.FirstOrDefault() ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Term Life", Value = "term_life" },
                    new CardChoice { Title = "Whole Life", Value = "whole_life" },
                    new CardChoice { Title = "Final Expense", Value = "final_expense" },
                    new CardChoice { Title = "Health Insurance", Value = "health_insurance" },
                    new CardChoice { Title = "Medicare", Value = "medicare" },
                    new CardChoice { Title = "Other", Value = "other" }
                }
            },

            // Coverage Start Time Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "⏱️ When are you looking to start coverage?",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "coverage_start_time",
                Value = coverageStartTime ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "ASAP", Value = "asap" },
                    new CardChoice { Title = "Within 30 Days", Value = "30_days" },
                    new CardChoice { Title = "1–3 Months", Value = "1_3_months" },
                    new CardChoice { Title = "3+ Months", Value = "3_months_plus" }
                }
            },

            // Coverage Amount Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "💵 Desired coverage amount?",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "coverage_amount",
                Value = desiredCoverageAmount ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Under $50k", Value = "under_50k" },
                    new CardChoice { Title = "$50k–$100k", Value = "50k_100k" },
                    new CardChoice { Title = "$100k–$250k", Value = "100k_250k" },
                    new CardChoice { Title = "Over $250k", Value = "over_250k" },
                    new CardChoice { Title = "Not Sure", Value = "not_sure" }
                }
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