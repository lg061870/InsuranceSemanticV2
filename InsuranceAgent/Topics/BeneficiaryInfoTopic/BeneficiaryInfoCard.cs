using ConversaCore.Cards;
using System;
using System.Collections.Generic;

namespace InsuranceAgent.Cards
{
    public class BeneficiaryInfoCard {
        public AdaptiveCardModel Create(
            string? name = "",
            string? relation = "",
            string? dob = "",
            int percentage = 0,
            string? progressText = null) {
            
            var bodyElements = new List<CardElement>
            {
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "👤 Beneficiary Information",
                    Weight = "Bolder",
                    Size = "Medium"
                }
            };

            // Add progress indicator if provided
            if (!string.IsNullOrEmpty(progressText))
            {
                bodyElements.Add(new CardElement
                {
                    Type = "TextBlock",
                    Text = $"📊 {progressText}",
                    Color = "Accent",
                    Size = "Small",
                    Wrap = true
                });
            }

            // Add the rest of the form elements
            bodyElements.AddRange(new List<CardElement>
            {
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "📛 Full Name of Beneficiary",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Text",
                    Id = "beneficiary_name",
                    Text = "Enter full name",
                    Value = name ?? ""
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "👥 Relationship to You",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.ChoiceSet",
                    Id = "beneficiary_relation",
                    Choices = new List<CardChoice>
                    {
                        new CardChoice { Title = "Spouse/Partner", Value = "Spouse/Partner" },
                        new CardChoice { Title = "Child", Value = "Child" },
                        new CardChoice { Title = "Parent", Value = "Parent" },
                        new CardChoice { Title = "Sibling", Value = "Sibling" },
                        new CardChoice { Title = "Other", Value = "Other" }
                    },
                    Value = relation ?? ""
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "🎂 Beneficiary's Date of Birth",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Date",
                    Id = "beneficiary_dob",
                    Value = dob ?? ""
                },
                new CardElement
                {
                    Type = "TextBlock",
                    Text = "💯 Beneficiary Percentage",
                    Wrap = true
                },
                new CardElement
                {
                    Type = "Input.Number",
                    Id = "beneficiary_percentage",
                    Value = percentage.ToString()
                }
            });

            var card = new AdaptiveCardModel {
                Type = "AdaptiveCard",
                Version = "1.5",
                Schema = "https://adaptivecards.io/schemas/adaptive-card.json",
                Body = bodyElements,
                Actions = new List<CardAction>
                {
                    new CardAction
                    {
                        Type = "Action.Submit",
                        Title = "➡️ Next"
                    }
                }
            };

            return card;
        }
    }

}
