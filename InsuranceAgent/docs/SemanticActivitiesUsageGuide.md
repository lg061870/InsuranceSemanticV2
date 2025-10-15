# Semantic Activities Framework - Usage Guide

This guide demonstrates how to integrate the new semantic activities framework into your ConversaCore TopicFlow workflows for insurance scenarios.

## Quick Start

### 1. Basic Insurance Decision Activity

```csharp
// Add to your TopicFlow
Add(new SimpleActivity("LoadUserProfile", LoadUserProfileFromContext))
    .AddInsuranceDecision(
        activityId: "AnalyzeEligibility",
        kernel: _kernel,
        logger: _logger,
        rulesFilePath: @"C:\path\to\TERM_INS_RULES.json",
        userProfileContextKey: "UserProfile"
    )
    .Add(new SimpleActivity("ProcessResults", HandleInsuranceResults));
```

### 2. Semantic Response for Conversation Redirection

```csharp
// Redirect off-topic conversations back to insurance
.AddSemanticResponse(
    activityId: "RedirectToInsurance",
    kernel: _kernel,
    logger: _logger,
    redirectionInstruction: "Gently guide the conversation back to life insurance options"
);
```

### 3. General AI Prompt Activity

```csharp
// Custom AI interaction with JSON output
.AddPrompt(
    activityId: "GenerateRecommendations", 
    kernel: _kernel,
    logger: _logger,
    systemPrompt: "You are an insurance expert providing personalized advice.",
    userPromptTemplate: "Based on user age {context.Age} and health status {context.HealthStatus}, recommend insurance products.",
    requireJsonOutput: true
);
```

## Advanced Configuration

### Fluent API Configuration

```csharp
// Advanced semantic response with fluent configuration
.AddSemanticResponse("FlexibleRedirect", _kernel, _logger, activity =>
{
    activity.WithInstruction("Acknowledge their concern but redirect to insurance benefits")
            .WithFocus("Term Life Insurance")
            .WithContext("The user is researching financial planning options")
            .WithTemperature(0.8f)
            .WithModel("gpt-4o");
});

// Custom prompt with specific configuration
.AddPrompt("PersonalizedAdvice", _kernel, _logger, activity =>
{
    activity.WithSystemPrompt("You are a licensed insurance advisor specializing in life insurance")
            .WithUserPrompt("Analyze this profile: {context.UserProfile}")
            .WithTemperature(0.3f)  // Lower temperature for factual analysis
            .RequireJsonOutput(true)
            .WithModel("gpt-4o-mini");
});
```

## User Profile Structure

Your user profiles should follow this structure for optimal insurance decision making:

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
  "desiredCoverage": 500000,
  "currentAge": 35
}
```

## Context Keys and Results

### Insurance Decision Activity Results

After running an `InsuranceDecisionActivity`, these context keys are available:

```csharp
// Primary decision result
var decision = context.GetValue<InsuranceDecisionResult>("{activityId}_Decision");

// Count metrics
var perfectMatchCount = context.GetValue<int>("{activityId}_PerfectMatchCount");
var nearMatchCount = context.GetValue<int>("{activityId}_NearMatchCount");

// Individual matches
foreach (var match in decision.PerfectMatches)
{
    Console.WriteLine($"Carrier: {match.CarrierName}");
    Console.WriteLine($"Product: {match.ProductType}");
    Console.WriteLine($"Reasoning: {match.Reasoning}");
}
```

### Semantic Response Activity Results

```csharp
// Get the AI-generated response
var response = context.GetValue<string>("{activityId}_Response");

// Check if redirection was successful
var wasRedirected = !string.IsNullOrEmpty(response);
```

### Prompt Activity Results

```csharp
// Raw AI response
var aiResponse = context.GetValue<string>("{activityId}_Response");

// If JSON was requested and successfully parsed
var jsonResult = context.GetValue<object>("{activityId}_JsonResult");
```

## Complete Topic Example

```csharp
public class InsuranceAnalysisTopic : TopicFlow
{
    private readonly Kernel _kernel;
    
    public InsuranceAnalysisTopic(
        TopicWorkflowContext context,
        ILogger<InsuranceAnalysisTopic> logger,
        IConversationContext conversationContext,
        Kernel kernel) : base(context, logger, conversationContext)
    {
        _kernel = kernel;
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Step 1: Gather user information (existing adaptive card activities)
        Add(new AdaptiveCardActivity<UserInfoCard, UserInfoModel>("CollectInfo"))
        
        // Step 2: Analyze insurance eligibility using AI
        .AddInsuranceDecision(
            "AnalyzeOptions",
            _kernel,
            _logger,
            @"C:\path\to\TERM_INS_RULES.json",
            "UserProfile"
        )
        
        // Step 3: Generate personalized recommendations
        .AddPrompt(
            "CreateRecommendations",
            _kernel,
            _logger,
            "You are an insurance advisor. Create personalized recommendations.",
            "Based on the analysis results: {context.AnalyzeOptions_Decision}, create 3 specific recommendations for this user profile: {context.UserProfile}",
            requireJsonOutput: true
        )
        
        // Step 4: Handle any off-topic responses
        .AddSemanticResponse(
            "HandleOffTopic",
            _kernel,
            _logger,
            "If the user asks about anything other than insurance, politely redirect them back to discussing their insurance options and next steps."
        )
        
        // Step 5: Present final recommendations
        .Add(new SimpleActivity("PresentRecommendations", DisplayFinalRecommendations));
    }
    
    private async Task<ActivityResult> DisplayFinalRecommendations(TopicWorkflowContext context, object? input)
    {
        var decision = context.GetValue<InsuranceDecisionResult>("AnalyzeOptions_Decision");
        var recommendations = context.GetValue<string>("CreateRecommendations_Response");
        
        // Combine AI analysis with rule-based decisions
        var finalReport = $@"
Insurance Analysis Complete!

Rule-Based Analysis:
- Perfect Matches: {decision?.PerfectMatches?.Count ?? 0}
- Near Matches: {decision?.NearMatches?.Count ?? 0}

AI Recommendations:
{recommendations}

Next Steps:
1. Review the recommended carriers
2. Request quotes from top matches
3. Schedule consultation with selected providers
";
        
        return ActivityResult.Continue(finalReport);
    }
    
    public override async Task<bool> CanHandleAsync(string input, IConversationContext conversationContext)
    {
        var keywords = new[] { "insurance", "coverage", "life insurance", "quotes" };
        return keywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
    
    protected override string[] IntentKeywords => new[]
    {
        "insurance analysis",
        "life insurance",
        "coverage options",
        "insurance quotes"
    };
}
```

## Error Handling

The semantic activities include comprehensive error handling:

```csharp
// Check if insurance analysis was successful
var decision = context.GetValue<InsuranceDecisionResult>("AnalyzeOptions_Decision");
if (decision == null)
{
    _logger.LogWarning("Insurance analysis failed - no decision result available");
    // Handle the error case
}

// Check for AI response failures
var aiResponse = context.GetValue<string>("CreateRecommendations_Response");
if (string.IsNullOrEmpty(aiResponse))
{
    _logger.LogWarning("AI recommendation generation failed");
    // Fallback to rule-based recommendations only
}
```

## Configuration Requirements

### DI Registration

Add to your `Program.cs` or DI configuration:

```csharp
// Required for semantic activities
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: "gpt-4o",
        apiKey: "your-api-key"
    );
    return kernelBuilder.Build();
});
```

### Topic Registration

```csharp
// In AddInsuranceTopics.cs
services.AddScoped<ITopic>(sp => new InsuranceAnalysisTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<InsuranceAnalysisTopic>>(),
    sp.GetRequiredService<IConversationContext>(),
    sp.GetRequiredService<Kernel>()
));
```

## Best Practices

1. **Use specific activity IDs** to avoid context key collisions
2. **Set appropriate temperatures**: 0.0-0.3 for factual analysis, 0.7-1.0 for creative responses
3. **Include error handling** for failed AI responses
4. **Validate user profiles** before passing to insurance decision activities
5. **Use JSON output** when you need structured data for further processing
6. **Test with real insurance scenarios** to validate rule matching accuracy

## Testing and Debugging

```csharp
// Add context dumping for debugging
.Add(new DumpCtxActivity("DebugAfterInsuranceAnalysis"))

// Log intermediate results
.Add(new SimpleActivity("LogResults", (ctx, input) =>
{
    var decision = ctx.GetValue<InsuranceDecisionResult>("AnalyzeOptions_Decision");
    _logger.LogInformation($"Found {decision?.PerfectMatches?.Count} perfect matches");
    return Task.FromResult(ActivityResult.Continue());
}))
```

This framework provides a powerful foundation for AI-enhanced insurance workflows while maintaining the reliability and structure of the ConversaCore TopicFlow architecture.