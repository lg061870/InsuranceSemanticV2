using ConversaCore.Cards;

namespace InsuranceAgent.Topics;

/// <summary>
/// Adaptive card for collecting health information.
/// Ported from Copilot Studio JSON.
/// </summary>
public class HealthInfoCard
{
    public AdaptiveCardModel Create(
        bool? usesTobacco = null,
        List<string>? selectedConditions = null,
        bool? hasHealthInsurance = null,
        string? height = "",
        string? weight = "",
        string? overallHealthStatus = null,
        string? currentMedications = "",
        string? familyMedicalHistory = "")
    {
        selectedConditions ??= new List<string>();

        var bodyElements = new List<CardElement>
        {
            // Header
            new CardElement
            {
                Type = "TextBlock",
                Text = "🏥 Health Information",
                Weight = "Bolder",
                Size = "Medium",
                Color = "Dark"
            },

            // Tobacco Use Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "🚬 Do you currently use tobacco products?",
                Wrap = true
            },

            // Tobacco Use Options
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "tobacco_use",
                Value = usesTobacco == true ? "yes" : (usesTobacco == false ? "no" : ""),
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Yes", Value = "yes" },
                    new CardChoice { Title = "No", Value = "no" }
                }
            },

            // Medical Conditions Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "🦠 Any of the following apply to you?",
                Wrap = true
            },

            // Medical Conditions Options - Row 1
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "condition_diabetes",
                Text = "Diabetes",
                Value = selectedConditions.Contains("Diabetes") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "condition_heart",
                Text = "Heart Condition",
                Value = selectedConditions.Contains("Heart Condition") ? "true" : "false"
            },

            // Medical Conditions Options - Row 2
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "condition_bp",
                Text = "High Blood Pressure",
                Value = selectedConditions.Contains("High Blood Pressure") ? "true" : "false"
            },
            new CardElement
            {
                Type = "Input.Toggle",
                Id = "condition_none",
                Text = "None Apply",
                Value = selectedConditions.Contains("None Apply") ? "true" : "false"
            },

            // Health Insurance Question
            new CardElement
            {
                Type = "TextBlock",
                Text = "🩹 Do you currently have health insurance?",
                Wrap = true
            },

            // Health Insurance Options
            new CardElement
            {
                Type = "Input.TagSelect",
                Id = "health_insurance",
                Value = hasHealthInsurance == true ? "yes" : (hasHealthInsurance == false ? "no" : ""),
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Yes", Value = "yes" },
                    new CardChoice { Title = "No", Value = "no" }
                }
            },

            // Height Input
            new CardElement
            {
                Type = "TextBlock",
                Text = "📏 Height (e.g., 5'10)",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "height",
                Text = "Enter your height",
                Value = height ?? ""
            },

            // Weight Input
            new CardElement
            {
                Type = "TextBlock",
                Text = "⚖️ Weight (e.g., 160 lbs)",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "weight",
                Text = "Enter your weight",
                Value = weight ?? ""
            },

            // Overall Health Status
            new CardElement
            {
                Type = "TextBlock",
                Text = "💪 Overall Health Status",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.ChoiceSet",
                Id = "overall_health_status",
                Value = overallHealthStatus ?? "",
                Choices = new List<CardChoice>
                {
                    new CardChoice { Title = "Excellent", Value = "Excellent" },
                    new CardChoice { Title = "Good", Value = "Good" },
                    new CardChoice { Title = "Fair", Value = "Fair" },
                    new CardChoice { Title = "Poor", Value = "Poor" }
                }
            },

            // Current Medications
            new CardElement
            {
                Type = "TextBlock",
                Text = "💊 Current Medications (if any)",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "current_medications",
                Placeholder = "List any medications you're currently taking",
                IsMultiline = true,
                Value = currentMedications ?? ""
            },

            // Family Medical History
            new CardElement
            {
                Type = "TextBlock",
                Text = "👨‍👩‍👧‍👦 Family Medical History (if any)",
                Wrap = true
            },
            new CardElement
            {
                Type = "Input.Text",
                Id = "family_medical_history",
                Placeholder = "Note any significant family medical history",
                IsMultiline = true,
                Value = familyMedicalHistory ?? ""
            },

            // Consent Notice
            new CardElement
            {
                Type = "TextBlock",
                Text = "🔒 By continuing, you consent to share this general health info for insurance qualification purposes.",
                Wrap = true,
                IsSubtle = true
            }
        };

        var actions = new List<CardAction>
        {
            new CardAction
            {
                Type = "Action.Submit",
                Title = "➡️ Next"
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