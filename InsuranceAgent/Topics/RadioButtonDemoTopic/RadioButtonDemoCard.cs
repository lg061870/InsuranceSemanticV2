using System;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.RadioButtonDemoTopic
{
    /// <summary>
    /// Demonstrates various radio button validation scenarios
    /// </summary>
    public class RadioButtonDemoCard
    {
        public object Create(bool? hasInsurance = null, bool? hasHome = null, bool? hasCar = null)
        {
            // Store as string for AdaptiveCard binding
            var hasInsuranceStr = hasInsurance.HasValue ? (hasInsurance.Value ? "true" : "false") : "";
            var hasHomeStr = hasHome.HasValue ? (hasHome.Value ? "true" : "false") : "";
            var hasCarStr = hasCar.HasValue ? (hasCar.Value ? "true" : "false") : "";

            return new Dictionary<string, object>
            {
                ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                ["type"] = "AdaptiveCard",
                ["version"] = "1.5",
                ["body"] = new object[]
                {
                    new {
                        type = "TextBlock",
                        text = "Radio Button Validation Demo",
                        weight = "Bolder",
                        size = "Medium",
                        wrap = true
                    },
                    new {
                        type = "TextBlock",
                        text = "Please answer all of the following questions:",
                        wrap = true
                    },

                    // Required radio button group - insurance
                    new Dictionary<string, object> {
                        ["type"] = "Input.ChoiceSet",
                        ["id"] = "hasInsurance",
                        ["label"] = "Do you currently have insurance?",
                        ["style"] = "expanded",
                        ["isMultiSelect"] = false,
                        ["isRequired"] = true,
                        ["value"] = hasInsuranceStr,
                        ["choices"] = new[] {
                            new { title = "Yes, I have insurance", value = "true" },
                            new { title = "No, I don't have insurance", value = "false" }
                        }
                    },

                    // Required radio button group - home
                    new Dictionary<string, object> {
                        ["type"] = "Input.ChoiceSet",
                        ["id"] = "hasHome",
                        ["label"] = "Do you own a home?",
                        ["style"] = "expanded",
                        ["isMultiSelect"] = false,
                        ["isRequired"] = true,
                        ["value"] = hasHomeStr,
                        ["choices"] = new[] {
                            new { title = "Yes, I own a home", value = "true" },
                            new { title = "No, I don't own a home", value = "false" }
                        }
                    },

                    // Required radio button group - car
                    new Dictionary<string, object> {
                        ["type"] = "Input.ChoiceSet",
                        ["id"] = "hasCar",
                        ["label"] = "Do you own a car?",
                        ["style"] = "expanded", 
                        ["isMultiSelect"] = false,
                        ["isRequired"] = true,
                        ["value"] = hasCarStr,
                        ["choices"] = new[] {
                            new { title = "Yes, I own a car", value = "true" },
                            new { title = "No, I don't own a car", value = "false" }
                        }
                    }
                },
                ["actions"] = new object[]
                {
                    new {
                        type = "Action.Submit",
                        title = "Submit",
                        style = "positive",
                        data = new { action = "submitRadioButtonDemo" }
                    }
                }
            };
        }
    }
}