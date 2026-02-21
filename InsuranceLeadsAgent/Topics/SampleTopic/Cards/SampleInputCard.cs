using System.Collections.Generic;
using ConversaCore.Cards;

namespace InsuranceLeadsAgent.Topics.SampleTopic.Cards
{
    /// <summary>
    /// Minimal adaptive card for collecting a single free-form question or message
    /// from the user as part of the sample topic.
    /// </summary>
    public class SampleInputCard
    {
        public AdaptiveCardModel Create(string? question = "")
        {
            var body = new List<CardElement>
            {
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Tell me what you would like to explore.",
                    Weight = "Bolder",
                    Size = "Medium"
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "This is a generic sample card. In your own app, you can replace it with a domain-specific form.",
                    Wrap = true,
                    IsSubtle = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "question",
                    Placeholder = "Type your question or message here...",
                    Value = question ?? string.Empty
                }
            };

            var actions = new List<CardAction>
            {
                new CardAction
                {
                    Type = "Action.Submit",
                    Title = "Submit",
                    Data = new { action = "submit" }
                }
            };

            return new AdaptiveCardModel
            {
                Body = body,
                Actions = actions
            };
        }
    }
}
