using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Adaptive card for low-intent scenarios with minimal educational content.
    /// Scenarios: TCPA: No + CCPA: No, TCPA: Don't Answer + CCPA: No
    /// </summary>
    public class LowIntentScenarioCard
    {
        public AdaptiveCardModel Create()
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "� Limited Resources Available",
                    Weight = "Bolder",
                    Size = "Medium",
                    Color = "Attention"
                },

                // Status explanation
                new CardElement
                {
                    Type = "Container",
                    Style = "attention",
                    Items = new List<CardElement>
                    {
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "🔒 **Your Privacy Status**: You declined both calls/texts and privacy acknowledgment",
                            Weight = "Bolder",
                            Wrap = true
                        },
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "**What this means**: To respect your privacy preferences and comply with federal regulations, we can only provide general educational content without any personal contact or customized services.",
                            Wrap = true,
                            IsSubtle = true
                        }
                    }
                },

                // Simple message
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Available resources (no personal information required):",
                    Weight = "Bolder",
                    Wrap = true
                },

                // Limited options
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "user_choice",
                    Text = "Where would you like to go?",
                    Style = "expanded",
                    Value = "insurance_basics",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "� Insurance basics and terminology", Value = "insurance_basics" },
                        new CardChoice { Title = "💡 Coverage type guides", Value = "coverage_guides" },
                        new CardChoice { Title = "🧮 Premium calculators", Value = "calculators" },
                        new CardChoice { Title = "🏠 Return to homepage", Value = "go_home" },
                        new CardChoice { Title = "🔄 Change my privacy preferences", Value = "change_consent" }
                    }
                },

                // Educational note
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💡 Want personalized quotes and agent assistance? You can update your consent preferences to unlock full services.",
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