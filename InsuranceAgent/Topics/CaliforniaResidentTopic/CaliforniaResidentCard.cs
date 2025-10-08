namespace InsuranceAgent.Cards {
    /// <summary>
    /// Defines the California Resident adaptive card schema.
    /// Serialized to JSON before being sent to the chat window.
    /// Validation errors are injected dynamically by AdaptiveCardValidationHelper,
    /// so this schema does NOT contain static errorMessage fields.
    /// </summary>
    public class CaliforniaResidentCard {
        /// <param name="isResident">Initial value for residency.</param>
        /// <param name="zip">Initial ZIP (only applies when isResident is true).</param>
        public object Create(bool? isResident = null, string? zip = null) {
            // Store as string for AdaptiveCard binding
            var isResidentStr = isResident.HasValue ? (isResident.Value ? "true" : "false") : "";

            return new Dictionary<string, object> {
                ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                ["type"] = "AdaptiveCard",
                ["version"] = "1.5",
                ["body"] = new object[] {
                    new {
                        type   = "TextBlock",
                        text   = "California Residency Confirmation",
                        weight = "Bolder",
                        size   = "Medium",
                        wrap   = true
                    },
                    new {
                        type = "TextBlock",
                        text = "Please confirm whether you are a California resident:",
                        wrap = true
                    },

                    // Explicit Yes/No ChoiceSet
                    new Dictionary<string, object> {
                        ["type"]        = "Input.ChoiceSet",
                        ["id"]          = "isCaliforniaResident",
                        ["style"]       = "expanded",
                        ["isMultiSelect"]= false,
                        ["value"]       = isResidentStr, // pre-fill if known
                        ["choices"]     = new[] {
                            new { title = "Yes, I am a California resident", value = "true" },
                            new { title = "No, I am not a California resident", value = "false" }
                        }
                    },
                    new Dictionary<string, object> {
                        ["type"]          = "Input.Text",
                        ["id"]            = "zipCode",
                        ["placeholder"]   = "e.g., 94107",
                        ["value"]         = zip ?? "",
                        // Validation hints consumed by your validation layer
                        ["isRequired"]    = true,
                        ["isRequiredWhen"]= "isCaliforniaResident:true",
                        ["regex"]         = @"^\d{5}$"
                        // ⚠️ removed ["errorMessage"]
                    }
                },
                ["actions"] = new object[] {
                    new {
                        type  = "Action.Submit",
                        title = "Submit",
                        style = "positive",
                        data  = new { action = "submitCaliforniaResident" }
                    }
                }
            };
        }
    }
}
