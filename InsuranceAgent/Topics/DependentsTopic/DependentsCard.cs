using ConversaCore.Cards;

namespace InsuranceAgent.Topics;

/// <summary>
/// Adaptive card for collecting dependents information.
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
            // ---------------------------
            // Header
            // ---------------------------
            new CardElement
            {
                Type = "TextBlock",
                Text = "👨‍👩‍👧‍👦 Who depends on you financially?",
                Weight = "Bolder",
                Size = "Medium"
            },

            // ---------------------------
            // Marital Status
            // ---------------------------
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

            // ---------------------------
            // Dependents Yes / No
            // ---------------------------
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

            // ---------------------------
            // Number of Children
            // ---------------------------
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

            // ---------------------------
            // Age Ranges
            // ---------------------------
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
                new CardAction
                {
                    Type = "Action.Submit",
                    Title = "➡️ Next"
                }
            }
        };
    }
}
