using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Adaptive card for blocked scenarios with maximum restrictions.
    /// Scenarios: TCPA: No + CCPA: No, TCPA: Don't Answer + CCPA: No (maximum restriction cases)
    /// </summary>
    public class BlockedScenarioCard
    {
        public AdaptiveCardModel Create()
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🚫 Maximum Privacy Mode",
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
                            Text = "🚫 **Your Status**: You declined contact permission and didn't respond to privacy disclosures",
                            Weight = "Bolder",
                            Wrap = true
                        },
                        new CardElement
                        {
                            Type = "TextBlock",
                            Text = "**Legal Limitations**: Due to insurance licensing regulations and privacy laws, we cannot provide personalized services, quotes, or contact options without proper consent. We can only offer basic website navigation.",
                            Wrap = true,
                            IsSubtle = true
                        }
                    }
                },

                // Simple message
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Available options:",
                    Weight = "Bolder",
                    Wrap = true
                },

                // Minimal options
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "user_choice",
                    Text = "What would you like to do?",
                    Style = "expanded",
                    Value = "homepage",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "🏠 Return to homepage", Value = "homepage" },
                        new CardChoice { Title = "� View general information", Value = "general_info" },
                        new CardChoice { Title = "🔍 Use site search", Value = "site_search" },
                        new CardChoice { Title = "🔄 Review consent options", Value = "change_consent" }
                    }
                },

                // Encouragement note
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💡 **Want full insurance services?** Providing consent allows us to offer personalized quotes, agent consultations, and comprehensive coverage options.",
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