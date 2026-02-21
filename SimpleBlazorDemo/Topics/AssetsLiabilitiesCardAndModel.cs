using ConversaCore.Cards;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleBlazorDemo.Topics;

public class AssetsLiabilitiesCard {
    public AdaptiveCardModel Create(
        string? hasHomeEquity = "",
        string? homeEquityAmount = "",
        string? savingsAmount = "",
        string? investmentsAmount = "",
        string? retirementAmount = "",
        string? creditCardDebt = "",
        string? studentLoans = "",
        string? autoLoans = "",
        string? mortgageDebt = "",
        string? otherDebt = "") {
        var rangeChoices = new List<CardChoice>
        {
            new() { Title = "None", Value = "0_10k" },
            new() { Title = "$10k–$25k", Value = "10k_25k" },
            new() { Title = "$25k–$50k", Value = "25k_50k" },
            new() { Title = "$50k–$100k", Value = "50k_100k" },
            new() { Title = "$100k–$250k", Value = "100k_250k" },
            new() { Title = "$250k+", Value = "250k_plus" }
        };

        var yesNoChoices = new List<CardChoice>
        {
            new() { Title = "Yes", Value = "yes" },
            new() { Title = "No", Value = "no" }
        };

        var body = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "💼 Assets & Liabilities",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "🏠 Do you own a home or have home equity?"
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "has_home_equity",
                Value = hasHomeEquity ?? "",
                Choices = yesNoChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "🏡 Approximate Home Equity",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "home_equity_amount",
                Value = homeEquityAmount ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "💰 Savings",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "savings_amount",
                Value = savingsAmount ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "📈 Investments",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "investments_amount",
                Value = investmentsAmount ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "🧓 Retirement Accounts (401k, IRA, etc.)",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "retirement_amount",
                Value = retirementAmount ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "💳 Credit Card Debt",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "credit_card_debt",
                Value = creditCardDebt ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "🎓 Student Loans",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "student_loans",
                Value = studentLoans ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "🚗 Auto Loans",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "auto_loans",
                Value = autoLoans ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "🏡 Mortgage Debt",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "mortgage_debt",
                Value = mortgageDebt ?? "",
                Choices = rangeChoices
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "📦 Other Debt",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "other_debt",
                Value = otherDebt ?? "",
                Choices = rangeChoices
            }
        };

        return new AdaptiveCardModel {
            Type = "AdaptiveCard",
            Schema = "https://adaptivecards.io/schemas/adaptive-card.json",
            Version = "1.5",
            Body = body,
            Actions = new List<CardAction>
            {
                new CardAction { Type = "Action.Submit", Title = "➡️ Next" }
            }
        };
    }
}

public class AssetsLiabilitiesModel : BaseCardModel {
    [JsonPropertyName("has_home_equity")]
    public string? HasHomeEquityValue { get; set; }
    [JsonPropertyName("home_equity_amount")]
    public string? HomeEquityAmount { get; set; }
    [JsonPropertyName("savings_amount")]
    public string? SavingsAmount { get; set; }
    [JsonPropertyName("investments_amount")]
    public string? InvestmentsAmount { get; set; }
    [JsonPropertyName("retirement_amount")]
    public string? RetirementAmount { get; set; }
    [JsonPropertyName("credit_card_debt")]
    public string? CreditCardDebt { get; set; }
    [JsonPropertyName("student_loans")]
    public string? StudentLoans { get; set; }
    [JsonPropertyName("auto_loans")]
    public string? AutoLoans { get; set; }
    [JsonPropertyName("mortgage_debt")]
    public string? MortgageDebt { get; set; }
    [JsonPropertyName("other_debt")]
    public string? OtherDebt { get; set; }

    [JsonIgnore]
    public bool HasHomeEquity => HasHomeEquityValue?.ToLower() == "yes";

    private int ScoreRange(string? v) => v switch {
        "0_10k" => 1,
        "10k_25k" => 2,
        "25k_50k" => 3,
        "50k_100k" => 4,
        "100k_250k" => 5,
        "250k_plus" => 6,
        _ => 0
    };

    [JsonIgnore]
    public int TotalAssetScore =>
        ScoreRange(HomeEquityAmount) +
        ScoreRange(SavingsAmount) +
        ScoreRange(InvestmentsAmount) +
        ScoreRange(RetirementAmount);

    [JsonIgnore]
    public int TotalDebtScore =>
        ScoreRange(CreditCardDebt) +
        ScoreRange(StudentLoans) +
        ScoreRange(AutoLoans) +
        ScoreRange(MortgageDebt) +
        ScoreRange(OtherDebt);

    [JsonIgnore]
    public string NetWorthCategory => (TotalAssetScore - TotalDebtScore) switch {
        <= -2 => "High Debt / Low Assets",
        -1 => "Below Average",
        0 => "Average",
        1 => "Above Average",
        >= 2 => "Strong Financial Position"
    };

    [JsonIgnore]
    public int FinancialStabilityScore => Math.Clamp((TotalAssetScore * 10) - (TotalDebtScore * 7), 0, 100);

    [JsonIgnore]
    public string FinancialStabilityGrade => FinancialStabilityScore switch {
        >= 85 => "A",
        >= 70 => "B",
        >= 55 => "C",
        >= 40 => "D",
        _ => "E"
    };
}
