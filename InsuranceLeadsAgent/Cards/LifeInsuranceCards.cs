namespace InsuranceLeadsAgent.Cards;

public static class LifeInsuranceCards {
    // ============================================================
    // COMBINED PRE-QUALIFICATION CARD
    // ============================================================

    public static string PreQualificationCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Life Insurance Pre-Qualification",
      "weight": "Bolder",
      "size": "Large"
    },
    {
      "type": "TextBlock",
      "text": "Tell us a bit about yourself to estimate your eligibility.",
      "wrap": true
    },
    {
      "type": "TextBlock",
      "text": "What is your age?",
      "wrap": true
    },
    {
      "type": "Input.Number",
      "id": "age",
      "min": 18,
      "max": 85,
      "isRequired": true,
      "errorMessage": "Please enter an age between 18 and 85.",
      "placeholder": "Enter your age"
    },
    {
      "type": "TextBlock",
      "text": "Height (in inches)",
      "wrap": true
    },
    {
      "type": "Input.Number",
      "id": "height",
      "placeholder": "Height (in inches)",
      "isRequired": true,
      "errorMessage": "Height is required."
    },
    {
      "type": "TextBlock",
      "text": "Weight (in pounds)",
      "wrap": true
    },
    {
      "type": "Input.Number",
      "id": "weight",
      "placeholder": "Weight (in pounds)",
      "isRequired": true,
      "errorMessage": "Weight is required."
    },
    {
      "type": "TextBlock",
      "text": "Select any conditions that apply:",
      "wrap": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "conditions",
      "isMultiSelect": true,
      "choices": [
        { "title": "Heart Disease", "value": "HeartDisease" },
        { "title": "Cancer", "value": "Cancer" },
        { "title": "Diabetes", "value": "Diabetes" },
        { "title": "Stroke", "value": "Stroke" },
        { "title": "None of the above", "value": "None" }
      ]
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";

    // ============================================================
    // AGE CARD
    // ============================================================

    public static string AgeCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Life Insurance Pre-Qualification",
      "weight": "Bolder",
      "size": "Large"
    },
    {
      "type": "TextBlock",
      "text": "What is your age?",
      "wrap": true
    },
    {
      "type": "Input.Number",
      "id": "age",
      "min": 18,
      "max": 85,
      "isRequired": true,
      "errorMessage": "Please enter an age between 18 and 85.",
      "placeholder": "Enter your age"
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";


    // ============================================================
    // SMOKING STATUS CARD
    // ============================================================

    public static string SmokingCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Tobacco Use",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "TextBlock",
      "text": "Have you used tobacco or nicotine products in the past 12 months?",
      "wrap": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "isSmoker",
      "style": "expanded",
      "isRequired": true,
      "errorMessage": "Please select Yes or No.",
      "choices": [
        { "title": "Yes", "value": "Yes" },
        { "title": "No", "value": "No" }
      ]
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";


    // ============================================================
    // HEIGHT & WEIGHT CARD
    // ============================================================

    public static string HeightWeightCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Height & Weight",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "Input.Number",
      "id": "height",
      "placeholder": "Height (in inches)",
      "isRequired": true,
      "errorMessage": "Height is required."
    },
    {
      "type": "Input.Number",
      "id": "weight",
      "placeholder": "Weight (in pounds)",
      "isRequired": true,
      "errorMessage": "Weight is required."
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";


    // ============================================================
    // MEDICAL CONDITIONS CARD
    // ============================================================

    public static string MedicalConditionsCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Medical History",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "TextBlock",
      "text": "Select any conditions that apply:",
      "wrap": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "conditions",
      "isMultiSelect": true,
      "choices": [
        { "title": "Heart Disease", "value": "HeartDisease" },
        { "title": "Cancer", "value": "Cancer" },
        { "title": "Diabetes", "value": "Diabetes" },
        { "title": "Stroke", "value": "Stroke" },
        { "title": "None of the above", "value": "None" }
      ]
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";


    // ============================================================
    // HOSPITALIZATION HISTORY CARD
    // ============================================================

    public static string HospitalizationCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Recent Hospitalizations",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "TextBlock",
      "text": "Have you been hospitalized in the past 5 years?",
      "wrap": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "hospitalized",
      "style": "expanded",
      "isRequired": true,
      "errorMessage": "Please select Yes or No.",
      "choices": [
        { "title": "Yes", "value": "Yes" },
        { "title": "No", "value": "No" }
      ]
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";


    // ============================================================
    // DUI / SUBSTANCE HISTORY CARD
    // ============================================================

    public static string SubstanceHistoryCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Driving & Substance History",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "TextBlock",
      "text": "Have you had a DUI or substance-related offense in the past 5 years?",
      "wrap": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "dui",
      "style": "expanded",
      "isRequired": true,
      "errorMessage": "Please select Yes or No.",
      "choices": [
        { "title": "Yes", "value": "Yes" },
        { "title": "No", "value": "No" }
      ]
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";


    // ============================================================
    // OCCUPATION RISK CARD
    // ============================================================

    public static string OccupationCard() => """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "TextBlock",
      "text": "Occupation",
      "weight": "Bolder",
      "size": "Medium"
    },
    {
      "type": "TextBlock",
      "text": "Select your occupation type:",
      "wrap": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "occupation",
      "style": "compact",
      "isRequired": true,
      "errorMessage": "Please select your occupation.",
      "choices": [
        { "title": "Office / Professional", "value": "Office" },
        { "title": "Construction / Manual Labor", "value": "ManualLabor" },
        { "title": "Pilot / Aviation", "value": "Pilot" },
        { "title": "Police / Firefighter", "value": "PublicSafety" },
        { "title": "Other", "value": "Other" }
      ]
    }
  ],
  "actions": [
    { "type": "Action.Submit", "title": "Continue" }
  ]
}
""";
}
