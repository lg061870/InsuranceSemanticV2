using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.CoverageIntentTopic
{
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

                // Coverage Type Options - Row 1
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_term_life",
                    Text = "Term Life",
                    Value = selectedCoverageTypes.Contains("Term Life") ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_whole_life",
                    Text = "Whole Life",
                    Value = selectedCoverageTypes.Contains("Whole Life") ? "true" : "false"
                },

                // Coverage Type Options - Row 2
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_final_expense",
                    Text = "Final Expense",
                    Value = selectedCoverageTypes.Contains("Final Expense") ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_health",
                    Text = "Health Insurance",
                    Value = selectedCoverageTypes.Contains("Health Insurance") ? "true" : "false"
                },

                // Coverage Type Options - Row 3
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_medicare",
                    Text = "Medicare",
                    Value = selectedCoverageTypes.Contains("Medicare") ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_other",
                    Text = "Other",
                    Value = selectedCoverageTypes.Contains("Other") ? "true" : "false"
                },

                // Coverage Start Time Question
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "⏱️ When are you looking to start coverage?",
                    Wrap = true
                },

                // Coverage Start Options - Row 1
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_start_asap",
                    Text = "ASAP",
                    Value = coverageStartTime == "ASAP" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_start_30days",
                    Text = "Within 30 Days",
                    Value = coverageStartTime == "Within 30 Days" ? "true" : "false"
                },

                // Coverage Start Options - Row 2
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_start_1_3mo",
                    Text = "1–3 Months",
                    Value = coverageStartTime == "1–3 Months" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_start_3mo_plus",
                    Text = "3+ Months",
                    Value = coverageStartTime == "3+ Months" ? "true" : "false"
                },

                // Coverage Amount Question
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💵 Desired coverage amount?",
                    Wrap = true
                },

                // Coverage Amount Options - Row 1
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_amt_under_50",
                    Text = "Under $50k",
                    Value = desiredCoverageAmount == "Under $50k" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_amt_50_100",
                    Text = "$50k–$100k",
                    Value = desiredCoverageAmount == "$50k–$100k" ? "true" : "false"
                },

                // Coverage Amount Options - Row 2
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_amt_100_250",
                    Text = "$100k–$250k",
                    Value = desiredCoverageAmount == "$100k–$250k" ? "true" : "false"
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_amt_over_250",
                    Text = "Over $250k",
                    Value = desiredCoverageAmount == "Over $250k" ? "true" : "false"
                },

                // Coverage Amount Options - Row 3
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "coverage_amt_unsure",
                    Text = "Not Sure",
                    Value = desiredCoverageAmount == "Not Sure" ? "true" : "false"
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