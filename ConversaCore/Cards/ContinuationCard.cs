using ConversaCore.Cards;
using System.Collections.Generic;

namespace ConversaCore.Cards
{
    /// <summary>
    /// Simple continuation prompt card for RepeatActivity.
    /// Asks the user if they want to continue with another iteration.
    /// </summary>
    public class ContinuationCard : AdaptiveCardModel
    {
        public ContinuationCard(string promptText, int currentCount, string itemType = "item")
        {
            Type = "AdaptiveCard";
            Version = "1.3";

            Body = new List<CardElement>
            {
                // Progress indicator
                new CardElement
                {
                    Type = "Container",
                    Style = "emphasis",
                    Items = new List<CardElement>
                    {
                        new CardElement
                        {
                            Type = "TextBlock", 
                            Text = $"✅ {itemType} #{currentCount} completed!",
                            Weight = "Bolder",
                            Color = "Good",
                            Size = "Medium"
                        }
                    }
                },

                // Separator
                new CardElement
                {
                    Type = "TextBlock",
                    Text = " ",
                    Size = "Small"
                },

                // Prompt question
                new CardElement
                {
                    Type = "TextBlock",
                    Text = promptText,
                    Wrap = true,
                    Size = "Medium",
                    Weight = "Bolder"
                },

                // Choice set
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "user_response",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice 
                        { 
                            Title = $"➕ Yes, add another {itemType}", 
                            Value = "continue" 
                        },
                        new CardChoice 
                        { 
                            Title = $"✅ No, I'm done adding {itemType}s", 
                            Value = "stop" 
                        }
                    },
                    Value = "continue"  // Default to continue
                }
            };

            Actions = new List<CardAction>
            {
                new CardAction
                {
                    Type = "Action.Submit",
                    Title = "Continue"
                }
            };
        }
    }
}