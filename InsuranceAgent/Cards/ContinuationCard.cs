using ConversaCore.Cards;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Cards
{
    /// <summary>
    /// Card for asking user if they want to continue adding more items in a repeat flow
    /// </summary>
    public class ContinuationCard
    {
        public AdaptiveCardModel Create(
            int currentCount,
            string itemType = "item",
            string currentSummary = "",
            string promptText = "")
        {
            var bodyElements = new List<CardElement>
            {
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📊 Progress Update",
                    Weight = "Bolder",
                    Size = "Medium"
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = $"✅ You've added {currentCount} {itemType}{(currentCount == 1 ? "" : "s")}",
                    Color = "Good",
                    Wrap = true
                }
            };

            // Add current summary if provided
            if (!string.IsNullOrEmpty(currentSummary))
            {
                bodyElements.Add(new CardElement
                {
                    Type = "TextBlock",
                    Text = "📋 Current entries:",
                    Weight = "Bolder",
                    Wrap = true
                });
                
                bodyElements.Add(new CardElement
                {
                    Type = "TextBlock",
                    Text = currentSummary,
                    Wrap = true,
                    Size = "Small"
                });
            }

            // Add prompt text
            bodyElements.Add(new CardElement
            {
                Type = "TextBlock",
                Text = !string.IsNullOrEmpty(promptText) ? promptText : 
                       $"Would you like to add another {itemType}?",
                Wrap = true,
                Size = "Medium",
                Separator = true
            });

            // Add choice selection
            bodyElements.Add(new CardElement
            {
                Type = "Input.ChoiceSet",
                Id = "continue_choice",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = $"➕ Yes, add another {itemType}", Value = "yes" },
                    new CardChoice { Title = $"✅ No, I'm done adding {itemType}s", Value = "no" }
                },
                Value = "yes"
            });

            var card = new AdaptiveCardModel
            {
                Type = "AdaptiveCard",
                Version = "1.5",
                Schema = "https://adaptivecards.io/schemas/adaptive-card.json",
                Body = bodyElements,
                Actions = new List<CardAction>
                {
                    new CardAction
                    {
                        Type = "Action.Submit",
                        Title = "Continue"
                    }
                }
            };

            return card;
        }
    }

    /// <summary>
    /// Model for continuation decision
    /// </summary>
    public class ContinuationModel : BaseCardModel
    {
        [JsonPropertyName("continue_choice")]
        public string? ContinueChoice { get; set; }

        public bool ShouldContinue => ContinueChoice?.ToLowerInvariant() == "yes";
    }
}