using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Adaptive card for medium-intent scenarios where user is undecided on TCPA.
    /// Scenarios: TCPA: Don't Answer + CCPA: Yes/Don't Answer
    /// </summary>
    public class MediumIntentScenarioCard
    {
        public AdaptiveCardModel Create()
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📚 Educational Resources Available",
                    Weight = "Bolder",
                    Size = "Medium",
                    Color = "Warning"
                },

                // Status explanation
                new CardElement
                {
                    Type = "Container",
                    Style = "warning",
                    Items = new List<CardElement>
                    {
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "⚠️ **Your Consent Status**: You declined calls/texts but acknowledged California privacy rights",
                            Weight = "Bolder",
                            Wrap = true
                        },
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "**What this means**: By law, we cannot call or text you without TCPA consent. However, since you've acknowledged your privacy rights, we can still provide educational resources and optional email contact.",
                            Wrap = true,
                            IsSubtle = true
                        }
                    }
                },

                // Options explanation
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Here's what we can still do for you:",
                    Weight = "Bolder",
                    Wrap = true
                },

                // Options
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "user_choice",
                    Text = "How would you like to proceed?",
                    Style = "expanded",
                    Value = "view_guides",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "📖 View insurance guides and calculators", Value = "view_guides" },
                        new CardChoice { Title = "💬 Chat with an agent now (if available)", Value = "chat_agent" },
                        new CardChoice { Title = "📱 Schedule a convenient callback", Value = "schedule_callback" },
                        new CardChoice { Title = "✉️ Email me information (no calls)", Value = "email_info" },
                        new CardChoice { Title = "🏠 Return to homepage", Value = "go_home" }
                    }
                },

                // Educational note
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💡 All our educational resources are available without providing personal information",
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