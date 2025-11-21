using ConversaCore.Cards;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics;

/// <summary>
/// Model for collecting the user's assets, liabilities, and net worth profile.
/// </summary>
public class AssetsLiabilitiesModel : BaseCardModel {
    // -----------------------------
    // ASSETS
    // -----------------------------

    [JsonPropertyName("has_home_equity")]
    public string? HasHomeEquityValue { get; set; }   // yes/no

    [JsonPropertyName("home_equity_amount")]
    public string? HomeEquityAmount { get; set; }     // string range: "0_50k", "50k_100k", etc.

    [JsonPropertyName("savings_amount")]
    public string? SavingsAmount { get; set; }        // ranges

    [JsonPropertyName("investments_amount")]
    public string? InvestmentsAmount { get; set; }    // ranges

    [JsonPropertyName("retirement_amount")]
    public string? RetirementAmount { get; set; }     // ranges


    // -----------------------------
    // LIABILITIES
    // -----------------------------

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


    // -----------------------------
    // COMPUTED HELPERS
    // -----------------------------

    [JsonIgnore]
    public bool HasHomeEquity =>
        HasHomeEquityValue?.ToLower() == "yes";

    // Converts the ranges into simple scoring weights
    private int ScoreRange(string? v) {
        return v switch {
            "0_10k" => 1,
            "10k_25k" => 2,
            "25k_50k" => 3,
            "50k_100k" => 4,
            "100k_250k" => 5,
            "250k_plus" => 6,
            _ => 0
        };
    }

    // -----------------------------
    // FINANCIAL SCORING
    // -----------------------------

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
    public string NetWorthCategory =>
        (TotalAssetScore - TotalDebtScore) switch {
            <= -2 => "High Debt / Low Assets",
            -1 => "Below Average",
            0 => "Average",
            1 => "Above Average",
            >= 2 => "Strong Financial Position"
        };

    [JsonIgnore]
    public int FinancialStabilityScore =>
        Math.Clamp((TotalAssetScore * 10) - (TotalDebtScore * 7), 0, 100);

    [JsonIgnore]
    public string FinancialStabilityGrade =>
        FinancialStabilityScore switch {
            >= 85 => "A",
            >= 70 => "B",
            >= 55 => "C",
            >= 40 => "D",
            _ => "E"
        };

    [JsonIgnore]
    public List<string> UnderwritingFlags {
        get {
            var f = new List<string>();

            if (TotalDebtScore >= 12)
                f.Add("High Debt Load");

            if (TotalAssetScore < 4)
                f.Add("Low Asset Reserves");

            if (HasHomeEquity && MortgageDebt == "250k_plus")
                f.Add("High Mortgage Relative to Home Equity");

            return f;
        }
    }
}
