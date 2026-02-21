using ConversaCore.Cards;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Adaptive card for collecting dependents information.
/// Copied from InsuranceAgent.Topics for demo use.
/// </summary>
public class DependentsCard {
    public AdaptiveCardModel Create(
        string? maritalStatus = "",
        string? noOfChildren = "",
        bool? hasDependents = null,
        List<string>? selectedAgeRanges = null) {
        selectedAgeRanges ??= new List<string>();

        var body = new List<CardElement>
        {
            new CardElement
            {
                Type = "TextBlock",
                Text = "👨‍👩‍👧‍👦 Who depends on you financially?",
                Weight = "Bolder",
                Size = "Medium"
            },
            new CardElement { Type = "TextBlock", Text = "💍 Marital Status", Wrap = true },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "marital_status",
                Value = maritalStatus ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Single", Value = "Single" },
                    new CardChoice { Title = "Married", Value = "Married" },
                    new CardChoice { Title = "Partnered", Value = "Partnered" },
                    new CardChoice { Title = "Divorced", Value = "Divorced" },
                    new CardChoice { Title = "Widowed", Value = "Widowed" }
                }
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "👶 Children or Dependents?",
                Weight = "Bolder",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "has_dependents",
                Value = hasDependents == true ? "yes" : hasDependents == false ? "no" : "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Yes", Value = "yes" },
                    new CardChoice { Title = "No", Value = "no" }
                }
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "📊 Number of Children",
                Weight = "Bolder",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Number",
                Id = "no_of_children",
                Placeholder = "How many children?",
                Value = string.IsNullOrWhiteSpace(noOfChildren) ? null : noOfChildren
            },
            new CardElement
            {
                Type = "TextBlock",
                Text = "📊 Children's Age Ranges",
                Weight = "Bolder",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_0_5",
                Text = "0–5",
                Value = selectedAgeRanges.Contains("0-5") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_6_12",
                Text = "6–12",
                Value = selectedAgeRanges.Contains("6-12") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_13_17",
                Text = "13–17",
                Value = selectedAgeRanges.Contains("13-17") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_18_25",
                Text = "18–25",
                Value = selectedAgeRanges.Contains("18-25") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_25plus",
                Text = "Over 25",
                Value = selectedAgeRanges.Contains("Over 25") ? "true" : "false"
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

/// <summary>
/// Model for financial dependents form data.
/// Copied from InsuranceAgent.Topics for demo use.
/// </summary>
public class DependentsModel : BaseCardModel {
    [JsonPropertyName("marital_status")]
    public string? MaritalStatusValue { get; set; }
    public string MaritalStatus => MaritalStatusValue ?? "Not specified";

    [JsonPropertyName("has_dependents")]
    public string? HasDependentsValue { get; set; }
    public bool? HasDependents => HasDependentsValue?.ToLower() switch {
        "yes" => true,
        "no" => false,
        _ => null
    };

    [JsonPropertyName("no_of_children")]
    public string? NoOfChildren { get; set; }
    public int ChildrenCount {
        get {
            if (int.TryParse(NoOfChildren, out var n) && n >= 0) return n;
            return 0;
        }
    }

    [JsonPropertyName("ageRange_0_5")] public string AgeRange0To5 { get; set; } = "false";
    [JsonPropertyName("ageRange_6_12")] public string AgeRange6To12 { get; set; } = "false";
    [JsonPropertyName("ageRange_13_17")] public string AgeRange13To17 { get; set; } = "false";
    [JsonPropertyName("ageRange_18_25")] public string AgeRange18To25 { get; set; } = "false";
    [JsonPropertyName("ageRange_25plus")] public string AgeRange25Plus { get; set; } = "false";

    public List<string> SelectedAgeRanges {
        get {
            var list = new List<string>();
            if (AgeRange0To5 == "true") list.Add("0-5");
            if (AgeRange6To12 == "true") list.Add("6-12");
            if (AgeRange13To17 == "true") list.Add("13-17");
            if (AgeRange18To25 == "true") list.Add("18-25");
            if (AgeRange25Plus == "true") list.Add("Over 25");
            return list;
        }
    }

    public bool IsMarriedOrPartnered => MaritalStatus is "Married" or "Partnered";
    public bool IsSingleParent => (MaritalStatus is "Single" or "Divorced" or "Widowed") && HasDependents == true;
    public bool HasPartner => IsMarriedOrPartnered;
    public bool HasChildren => HasDependents == true;
    public bool IsChildlessCouple => IsMarriedOrPartnered && HasDependents == false;

    public bool HasYoungChildren => SelectedAgeRanges.Contains("0-5") || SelectedAgeRanges.Contains("6-12");
    public bool HasTeenageChildren => SelectedAgeRanges.Contains("13-17");
    public bool HasAdultChildren => SelectedAgeRanges.Contains("18-25") || SelectedAgeRanges.Contains("Over 25");
    public bool HasOnlyAdultChildren => HasAdultChildren && !HasYoungChildren && !HasTeenageChildren;

    public string FinancialResponsibilityLevel {
        get {
            if (IsSingleParent && HasYoungChildren) return "Very High";
            if (HasYoungChildren && IsMarriedOrPartnered) return "High";
            if (HasTeenageChildren) return "High";
            if (HasOnlyAdultChildren) return "Moderate";
            if (IsChildlessCouple) return "Moderate";
            if (MaritalStatus == "Single" && HasDependents == false) return "Low";
            if (HasDependents == false) return "Low";
            return "Unknown";
        }
    }

    public int EstimatedNumberOfDependents {
        get {
            if (HasDependents != true) return 0;
            if (ChildrenCount > 0) return ChildrenCount;
            int inferred = SelectedAgeRanges.Count;
            if (inferred == 0) return 1;
            if (inferred >= 4) return 3;
            return inferred;
        }
    }
}
