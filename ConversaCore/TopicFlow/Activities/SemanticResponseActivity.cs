using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ConversaCore.TopicFlow.Activities;

/// <summary>
/// Semantic activity for contextual conversation redirection.
/// Takes off-topic user input and provides a clever response that guides the conversation back to the intended topic.
/// </summary>
public class SemanticResponseActivity : SemanticActivity
{
    public string RedirectionInstruction { get; set; } = string.Empty;
    public string? ConversationContext { get; set; }
    public string? CurrentTopicFocus { get; set; }

    public SemanticResponseActivity(string activityId, Kernel kernel, ILogger logger) 
        : base(activityId, kernel, logger)
    {
        Temperature = 0.8f; // Higher temperature for more creative, natural responses
        RequireJsonOutput = false; // Natural language response
        ModelId = "gpt-4o-mini"; // Fast model for quick responses
    }

    protected override async Task<string> BuildSystemPromptAsync(TopicWorkflowContext context, object? input)
    {
        await Task.CompletedTask;

        var systemPrompt = @"You are a skilled conversation guide for an insurance chatbot. Your role is to acknowledge what the user said and then cleverly redirect the conversation back to insurance topics in a natural, helpful way.

## Your Objectives
1. **Acknowledge**: Show that you heard and understood what the user said
2. **Bridge**: Create a natural connection between their topic and insurance
3. **Redirect**: Guide them back to insurance discussion without being pushy or dismissive
4. **Maintain Rapport**: Keep the conversation friendly and engaging

## Style Guidelines
- Be conversational and natural
- Show empathy and understanding
- Use appropriate humor when suitable
- Make connections that feel genuine, not forced
- Keep responses concise but warm

## Response Pattern
1. Brief acknowledgment of their input
2. Creative bridge or connection to insurance
3. Gentle redirect with a question or suggestion

## Examples of Good Redirects
User: ""The sun is always shining""
Response: ""That's a wonderfully optimistic outlook! Speaking of sunshine, life insurance can provide that same kind of bright security for your family's future. Have you thought about what kind of coverage might give you that peace of mind?""

User: ""I love pizza""
Response: ""Pizza is amazing! You know, just like pizza protects you from hunger, insurance protects you from life's unexpected challenges. What aspects of protection are most important to you right now?""

## What NOT to Do
- Don't ignore what they said
- Don't be robotic or scripted
- Don't make the connection feel forced
- Don't be pushy or sales-y
- Don't dismiss their interests";

        if (!string.IsNullOrEmpty(RedirectionInstruction))
        {
            systemPrompt += $@"

## Specific Redirection Instruction
{RedirectionInstruction}";
        }

        if (!string.IsNullOrEmpty(CurrentTopicFocus))
        {
            systemPrompt += $@"

## Current Topic Focus
The conversation should be guided toward: {CurrentTopicFocus}";
        }

        return systemPrompt;
    }

    protected override async Task<string> BuildUserPromptAsync(TopicWorkflowContext context, object? input)
    {
        await Task.CompletedTask;

        var userInput = input?.ToString() ?? "I'm not sure what to say.";

        var userPrompt = $@"The user just said: ""{userInput}""

Please provide a natural, friendly response that acknowledges what they said and skillfully redirects the conversation back to insurance topics.";

        // Add conversation context if available
        if (!string.IsNullOrEmpty(ConversationContext))
        {
            userPrompt += $@"

## Previous Conversation Context
{ConversationContext}

Consider this context when crafting your response to maintain conversation continuity.";
        }

        // Check if there's additional context from the workflow
        var contextualInfo = ExtractContextualInfo(context);
        if (!string.IsNullOrEmpty(contextualInfo))
        {
            userPrompt += $@"

## Additional Context
{contextualInfo}";
        }

        userPrompt += @"

Remember to:
- Acknowledge their input genuinely
- Create a natural bridge to insurance
- Ask an engaging follow-up question
- Keep the tone conversational and helpful";

        return userPrompt;
    }

    private string ExtractContextualInfo(TopicWorkflowContext context)
    {
        var contextInfo = new List<string>();

        // Look for user profile information
        if (context.ContainsKey("UserProfile"))
        {
            contextInfo.Add("User profile information is available for personalization");
        }

        // Look for previous insurance decisions
        if (context.ContainsKey("InsuranceDecision"))
        {
            contextInfo.Add("Previous insurance analysis results are available");
        }

        // Look for collected user information
        if (context.ContainsKey("UserAge"))
        {
            contextInfo.Add($"User age: {context.GetValue<string>("UserAge")}");
        }

        if (context.ContainsKey("UserLocation"))
        {
            contextInfo.Add($"User location: {context.GetValue<string>("UserLocation")}");
        }

        // Look for current workflow state
        if (context.ContainsKey("CurrentWorkflowStep"))
        {
            contextInfo.Add($"Current workflow step: {context.GetValue<string>("CurrentWorkflowStep")}");
        }

        return contextInfo.Count > 0 ? string.Join("; ", contextInfo) : string.Empty;
    }

    protected override async Task StoreResultsInContextAsync(TopicWorkflowContext context, string response)
    {
        // Store the base result
        await base.StoreResultsInContextAsync(context, response);

        // Store as the last redirection response for potential follow-up
        context.SetValue("LastRedirectionResponse", response);
        context.SetValue("LastRedirectionTimestamp", DateTime.UtcNow);

        // Track redirection patterns for conversation flow optimization
        var redirectionCount = context.GetValue<int>("RedirectionCount");
        context.SetValue("RedirectionCount", redirectionCount + 1);

        _semanticLogger.LogDebug("Stored semantic response. Total redirections in this conversation: {RedirectionCount}", redirectionCount + 1);
    }

    /// <summary>
    /// Set conversation context for more personalized redirections
    /// </summary>
    public SemanticResponseActivity WithContext(string conversationContext)
    {
        ConversationContext = conversationContext;
        return this;
    }

    /// <summary>
    /// Set the specific topic focus for targeted redirections
    /// </summary>
    public SemanticResponseActivity WithFocus(string topicFocus)
    {
        CurrentTopicFocus = topicFocus;
        return this;
    }

    /// <summary>
    /// Set a specific redirection instruction
    /// </summary>
    public SemanticResponseActivity WithInstruction(string instruction)
    {
        RedirectionInstruction = instruction;
        return this;
    }
}