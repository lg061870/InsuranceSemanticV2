using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.InsuranceContextTopic
{
    /// <summary>
    /// Adaptive card for collecting insurance context and financial information.
    /// Ported from Copilot Studio JSON.
    /// </summary>
    public class InsuranceContextCard
    {
        public AdaptiveCardModel Create(
            string? insuranceType = "",
            string? coverageFor = "",
            string? coverageGoal = "",
            string? insuranceTarget = "",
            decimal? homeValue = null,
            decimal? mortgageBalance = null,
            decimal? monthlyMortgage = null,
            int? loanTerm = null,
            decimal? equity = null,
            bool? hasExistingLifeInsurance = null,
            string? existingCoverage = "")
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🛡️ Insurance Context",
                    Weight = "Bolder",
                    Size = "Medium"
                },

                // Insurance Type
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🔠 Type of Insurance",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "insurance_type",
                    Text = "e.g., Term Life, Whole Life",
                    Value = insuranceType ?? ""
                },

                // Coverage For
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "👥 Coverage For (Self, Spouse, etc.)",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "coverage_for",
                    Text = "e.g., Self, Spouse",
                    Value = coverageFor ?? ""
                },

                // Coverage Goal
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🎯 Coverage Goal",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "coverage_goal",
                    Text = "e.g., Mortgage Protection, Income Replacement",
                    Value = coverageGoal ?? ""
                },

                // Insurance Target
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🎯 Insurance Target (Coverage Band or Amount)",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "insurance_target",
                    Text = "e.g., $100,000",
                    Value = insuranceTarget ?? ""
                },

                // Home Value
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🏠 Home Value",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Number",
                    Id = "home_value",
                    Text = "e.g., 300000",
                    Value = homeValue?.ToString() ?? ""
                },

                // Mortgage Balance
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💳 Mortgage Balance",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Number",
                    Id = "mortgage_balance",
                    Text = "e.g., 200000",
                    Value = mortgageBalance?.ToString() ?? ""
                },

                // Monthly Mortgage
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💸 Monthly Mortgage",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Number",
                    Id = "monthly_mortgage",
                    Text = "e.g., 1500",
                    Value = monthlyMortgage?.ToString() ?? ""
                },

                // Loan Term
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🗕️ Loan Term (Years)",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Number",
                    Id = "loan_term",
                    Text = "e.g., 30",
                    Value = loanTerm?.ToString() ?? ""
                },

                // Equity
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💰 Equity in Property",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Number",
                    Id = "equity",
                    Text = "e.g., 100000",
                    Value = equity?.ToString() ?? ""
                },

                // Existing Life Insurance
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🧦 Do you have existing life insurance?",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "has_existing_life_insurance",
                    Text = "Yes",
                    Value = hasExistingLifeInsurance == true ? "true" : "false"
                },

                // Existing Coverage Amount
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📄 Existing Coverage Amount (if any)",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "existing_life_insurance_coverage",
                    Text = "e.g., $50,000",
                    Value = existingCoverage ?? ""
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