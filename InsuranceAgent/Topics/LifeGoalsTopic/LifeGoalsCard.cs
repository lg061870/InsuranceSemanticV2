using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.LifeGoalsTopic
{
    /// <summary>
    /// Adaptive card for collecting life insurance goals and intentions.
    /// Ported from Copilot Studio JSON with simplified layout.
    /// </summary>
    public class LifeGoalsCard
    {
        public AdaptiveCardModel Create(
            bool? protectLovedOnes = null,
            bool? payMortgage = null,
            bool? prepareFuture = null,
            bool? peaceOfMind = null,
            bool? coverExpenses = null,
            bool? unsure = null)
        {
            var bodyElements = new List<CardElement>
            {
                // Header
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Let's get started! What are your goals for life insurance?",
                    Weight = "Bolder",
                    Size = "ExtraLarge",
                    Wrap = true
                },

                // Instructions
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "Select all that apply.",
                    Wrap = true
                },

                // Protect Loved Ones
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "intent_protect_loved_ones",
                    Text = "👨‍👩‍👧‍👦 I want to protect my loved ones",
                    Value = protectLovedOnes == true ? "true" : "false"
                },

                // Pay Mortgage
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "intent_pay_mortgage",
                    Text = "🏠 I want to pay off my mortgage",
                    Value = payMortgage == true ? "true" : "false"
                },

                // Prepare Future
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "intent_prepare_future",
                    Text = "🛡️ I want to prepare for my family's future",
                    Value = prepareFuture == true ? "true" : "false"
                },

                // Peace of Mind
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "intent_peace_of_mind",
                    Text = "🧘 I'm looking for peace of mind",
                    Value = peaceOfMind == true ? "true" : "false"
                },

                // Cover Expenses
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "intent_cover_expenses",
                    Text = "📜 I want to cover my final expenses",
                    Value = coverExpenses == true ? "true" : "false"
                },

                // Unsure
                new CardElement
                {
                    Type = "Input.Toggle",
                    Id = "intent_unsure",
                    Text = "💭 I'm not sure",
                    Value = unsure == true ? "true" : "false"
                }
            };

            var actions = new List<CardAction>
            {
                new CardAction
                {
                    Type = "Action.Submit",
                    Title = "Submit"
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