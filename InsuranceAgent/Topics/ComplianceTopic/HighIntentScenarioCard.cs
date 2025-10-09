using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Adaptive card for high-intent scenarios where user declined TCPA but may want live agent contact.
    /// Scenarios: TCPA: No + CCPA: Yes/Don't Answer
    /// </summary>
    public class HighIntentScenarioCard
    {
        public AdaptiveCardModel Create()
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🎉 Great! Full Service Available",
                    Weight = "Bolder",
                    Size = "Medium",
                    Color = "Good"
                },

                // Status explanation
                new CardElement
                {
                    Type = "Container",
                    Style = "good",
                    Items = new List<CardElement>
                    {
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "✅ **Your Consent Status**: You've provided TCPA consent for calls and texts",
                            Weight = "Bolder",
                            Wrap = true
                        },
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "**What this means**: Since you've given us permission to contact you, we can offer our full range of personalized insurance services including direct agent calls, text updates, and customized quotes.",
                            Wrap = true,
                            IsSubtle = true
                        }
                    }
                },

                // Options explanation
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Here's what we can do for you:",
                    Weight = "Bolder",
                    Wrap = true
                },

                // Options
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "user_choice",
                    Text = "What would you like to do?",
                    Style = "expanded",
                    Value = "browse_info",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "� Call me now (agents available)", Value = "call_now" },
                        new CardChoice { Title = "💬 Live chat with agent", Value = "live_chat" },
                        new CardChoice { Title = "� Get personalized quote", Value = "get_quote" },
                        new CardChoice { Title = "� Browse information", Value = "browse_info" },
                        new CardChoice { Title = "🔄 Change my consent preferences", Value = "change_consent" }
                    }
                },

                // Information note
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💡 You can update your consent preferences at any time. Live agents are typically available Monday-Friday 9AM-6PM EST.",
                    IsSubtle = true,
                    Size = "Small",
                    Wrap = true
                }
            };

            var actions = new List<CardAction>
            {
                new CardAction
                {
                    Type = "Action.Submit",
                    Title = "Continue"
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
}