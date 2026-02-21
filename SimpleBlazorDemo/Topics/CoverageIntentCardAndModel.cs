using ConversaCore.Cards;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleBlazorDemo.Topics;

public class CoverageIntentCard {
    public AdaptiveCardModel Create(
        List<string>? selectedCoverageTypes = null,
        string? coverageStartTime = "",
        string? desiredCoverageAmount = "",
        string? monthlyBudget = "") {
        selectedCoverageTypes ??= new List<string>();

        var bodyElements = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "🎯 Coverage Intent",
                Weight = "Bolder",
                Size = "Medium",
                Color = "Dark"
            },
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
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "💰 Monthly budget for insurance?",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "monthly_budget",
                Value = monthlyBudget ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "$0–$50", Value = "0_50" },
                    new CardChoice { Title = "$50–$100", Value = "50_100" },
                    new CardChoice { Title = "$100–$150", Value = "100_150" },
                    new CardChoice { Title = "$150–$250", Value = "150_250" },
                    new CardChoice { Title = "$250–$400", Value = "250_400" },
                    new CardChoice { Title = "$400+", Value = "400_plus" },
                    new CardChoice { Title = "Not Sure", Value = "not_sure" }
                }
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction { Type = "Action.Submit", Title = "➡️ Next" }
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

public class CoverageIntentModel : BaseCardModel {
    [JsonPropertyName("coverage_type")]
    public string? CoverageType { get; set; }
    [JsonPropertyName("coverage_start_time")]
    public string? CoverageStartTime { get; set; }
    [JsonPropertyName("coverage_amount")]
    public string? CoverageAmount { get; set; }
    [JsonPropertyName("monthly_budget")]
    public string? MonthlyBudget { get; set; }

    public List<string> SelectedCoverageTypes {
        get {
            var types = new List<string>();
            if (!string.IsNullOrEmpty(CoverageType)) {
                var displayName = CoverageType switch {
                    "term_life" => "Term Life",
                    "whole_life" => "Whole Life",
                    "final_expense" => "Final Expense",
                    "health_insurance" => "Health Insurance",
                    "medicare" => "Medicare",
                    "other" => "Other",
                    _ => CoverageType
                };
                types.Add(displayName);
            }
            return types;
        }
    }

    public string PreferredCoverageStartTime => CoverageStartTime switch {
        "asap" => "ASAP",
        "30_days" => "Within 30 Days",
        "1_3_months" => "1–3 Months",
        "3_months_plus" => "3+ Months",
        _ => "Not specified"
    };

    public string DesiredCoverageAmountBand => CoverageAmount switch {
        "under_50k" => "Under $50k",
        "50k_100k" => "$50k–$100k",
        "100k_250k" => "$100k–$250k",
        "over_250k" => "Over $250k",
        "not_sure" => "Not Sure",
        _ => "Not specified"
    };

    public decimal? EstimatedCoverageAmount => DesiredCoverageAmountBand switch {
        "Under $50k" => 25000m,
        "$50k–$100k" => 75000m,
        "$100k–$250k" => 175000m,
        "Over $250k" => 500000m,
        "Not Sure" => null,
        _ => null
    };
}
