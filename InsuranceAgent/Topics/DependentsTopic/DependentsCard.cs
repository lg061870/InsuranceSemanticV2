using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics;

/// <summary>
/// Adaptive card for collecting financial dependents information.
/// Ported from Copilot Studio JSON.
/// </summary>
public class DependentsCard
{
    public AdaptiveCardModel Create(
        string? maritalStatus = "",
        bool? hasDependents = null,
        List<string>? selectedAgeRanges = null)
    {
        selectedAgeRanges ??= new List<string>();

        var bodyElements = new List<CardElement>
        {
            // Header
            new CardElement
            {
                Type = "TextBlock",
                Text = "👨‍👩‍👧‍👦 Who depends on you financially?",
                Weight = "Bolder",
                Size = "Medium",
                Color = "Dark"
            },

            // Marital Status Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "💍 Marital Status",
                Wrap = true
            },

            // Marital Status Options
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

            // Children/Dependents Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "👶 Children or Dependents?",
                Weight = "Bolder",
                Wrap = true
            },

            // Has Dependents Options
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "has_dependents",
                Value = hasDependents == true ? "yes" : (hasDependents == false ? "no" : ""),
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Yes", Value = "yes" },
                    new CardChoice { Title = "No", Value = "no" }
                }
            },

            // Age Ranges Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "📊 Children's Age Ranges",
                Weight = "Bolder",
                Wrap = true
            },

            // Age Range Options - Row 1
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_0_5",
                Text = "0-5",
                Value = selectedAgeRanges.Contains("0-5") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_6_12",
                Text = "6-12",
                Value = selectedAgeRanges.Contains("6-12") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_13_17",
                Text = "13-17",
                Value = selectedAgeRanges.Contains("13-17") ? "true" : "false"
            },

            // Age Range Options - Row 2
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "ageRange_18_25",
                Text = "18-25",
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