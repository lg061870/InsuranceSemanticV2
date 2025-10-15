# Testing Your Semantic Activities

## Quick Test Instructions

1. **Start the InsuranceAgent application**
2. **Type any of these trigger phrases:**
   - "semantic demo"
   - "ai decision"
   - "insurance analysis"
   - "semantic test"

3. **What happens:**
   - Sets up a mock user profile (35-year-old male, non-smoker, some medical history)
   - Analyzes insurance eligibility using your JSON rules
   - Demonstrates conversation redirection capabilities
   - Shows general AI prompt functionality

## Expected Output

```
User profile loaded for insurance analysis.

Insurance Analysis Complete!

Perfect Matches: [number]
Near Matches: [number]

Perfect Matches:
- Carrier Name (Product Type): Reasoning...

Near Matches:
- Carrier Name (Product Type): Reasoning...
  Exception: [exception condition]

[AI-generated response for redirection]

[AI-generated insurance advice in JSON format]

[Final flexible redirection response]
```

## Mock User Profile Structure

The demo uses this test profile:
```json
{
  "age": 35,
  "gender": "male", 
  "smokingStatus": "non-smoker",
  "medicalHistory": [
    {
      "condition": "diabetes_no_insulin",
      "diagnosisAge": 32,
      "yearsAgo": 3,
      "controlled": true
    },
    {
      "condition": "high_blood_pressure", 
      "controlled": true,
      "medications": 1
    }
  ],
  "legalHistory": [
    {
      "issue": "dui_one",
      "yearsAgo": 6
    }
  ],
  "employment": "full-time",
  "state": "california",
  "desiredCoverage": 500000
}
```

## Troubleshooting

If you get errors, check:

1. **Semantic Kernel service is registered** in `Program.cs`
2. **OpenAI API key is configured** 
3. **JSON rules files exist** at expected paths
4. **All semantic activity dependencies are resolved** in DI

## Next Steps

After successful testing:

1. **Replace mock profile** with real user data
2. **Customize prompts** for your specific insurance products
3. **Add error handling** for production scenarios
4. **Integrate with existing workflows** using the fluent API

The semantic activities framework is now ready for production use!