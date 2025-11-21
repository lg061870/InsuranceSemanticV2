using ConversaCore.Cards;

namespace InsuranceAgent.Topics;

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

            // HOME EQUITY
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

            // SAVINGS
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

            // INVESTMENTS
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

            // RETIREMENT
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

            // DEBTS SECTION
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
