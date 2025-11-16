using System.Collections.Generic;

namespace InsuranceAgent.Cards; 
/// <summary>
/// Adaptive card for confirming California residency and CCPA acknowledgment.
/// Displayed only if user ZIP is within California range.
/// </summary>
public class CaliforniaResidentCard {
    public object Create(bool? isResident = true, string? zip_code = "", string? ccpa_acknowledgment = "") {


        return new Dictionary<string, object> {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "🔒 California Resident Privacy Notice",
                    weight = "Bolder",
                    size = "Medium",
                    wrap = true
                },
                new
                {
                    type = "TextBlock",
                    text = "Under the California Consumer Privacy Act (CCPA), you have the right to know, delete, and opt-out of data sharing. We do not sell your personal information.",
                    wrap = true,
                    isSubtle = true
                },
                new
                {
                    type = "Input.ChoiceSet",
                    id = "ccpa_acknowledgment",
                    style = "compact",
                    value = ccpa_acknowledgment ?? "prefer_not_to_answer",
                    choices = new[]
                    {
                        new { title = "Yes, I acknowledge", value = "yes" },
                        new { title = "No, I do not acknowledge", value = "no" },
                        new { title = "Prefer not to answer", value = "prefer_not_to_answer" }
                    }
                },
                new
                {
                    type = "TextBlock",
                    text = "📍 ZIP Code",
                    wrap = true
                },
                new
                {
                    type = "Input.Text",
                    id = "zip_code",
                    value = zip_code ?? "",
                    placeholder = "Enter your ZIP code",
                    isRequired = true
                }
            },
            ["actions"] = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "✅ Submit",
                    style = "positive"
                }
            }
        };
    }
}
