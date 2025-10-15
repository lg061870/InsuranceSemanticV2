using ConversaCore.Context;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

namespace ConversaCore.TopicFlow.Activities;

/// <summary>
/// Base class for semantic activities that leverage AI/LLM capabilities within ConversaCore workflows.
/// Inherits from TopicFlowActivity for seamless integration with existing topic flows.
/// </summary>
public abstract class SemanticActivity : TopicFlowActivity
{
    protected readonly Kernel _kernel;
    protected readonly IChatCompletionService _chatCompletion;
    protected readonly ILogger _semanticLogger;

    // Configuration properties
    public string ModelId { get; set; } = "gpt-4o-mini";
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2048;
    public bool RequireJsonOutput { get; set; } = false;

    protected SemanticActivity(
        string activityId, 
        Kernel kernel, 
        ILogger logger) : base(activityId, null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _semanticLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
    }

    protected override async Task<ActivityResult> RunActivity(TopicWorkflowContext context, object? input = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _semanticLogger.LogInformation("Executing semantic activity {ActivityId}", Id);

            // Build the system prompt
            var systemPrompt = await BuildSystemPromptAsync(context, input);
            
            // Build the user prompt  
            var userPrompt = await BuildUserPromptAsync(context, input);

            // Prepare chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            // Configure execution settings
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ModelId = ModelId,
                Temperature = Temperature,
                MaxTokens = MaxTokens
            };

            // Add JSON formatting instruction if required
            if (RequireJsonOutput)
            {
                executionSettings.ResponseFormat = "json_object";
                chatHistory.AddSystemMessage("You must respond with valid JSON only. Do not include any text outside of the JSON object.");
            }

            _semanticLogger.LogDebug("Sending request to {ModelId} with temperature {Temperature}", ModelId, Temperature);

            // Execute the semantic operation
            var response = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings,
                cancellationToken: cancellationToken);

            var responseText = response.Content ?? string.Empty;

            _semanticLogger.LogDebug("Received response: {ResponseLength} characters", responseText.Length);

            // Process and validate the response
            var processedResponse = await ProcessResponseAsync(context, input, responseText);

            // Store results in context for downstream activities
            await StoreResultsInContextAsync(context, processedResponse);

            return ActivityResult.Continue(processedResponse, processedResponse);
        }
        catch (Exception ex)
        {
            _semanticLogger.LogError(ex, "Error executing semantic activity {ActivityId}", Id);
            return await HandleErrorAsync(context, input, ex);
        }
    }

    /// <summary>
    /// Build the system prompt that provides context and instructions to the AI model.
    /// Override this method to provide activity-specific system instructions.
    /// </summary>
    protected abstract Task<string> BuildSystemPromptAsync(TopicWorkflowContext context, object? input);

    /// <summary>
    /// Build the user prompt that contains the specific data and query for the AI model.
    /// Override this method to provide activity-specific user input formatting.
    /// </summary>
    protected abstract Task<string> BuildUserPromptAsync(TopicWorkflowContext context, object? input);

    /// <summary>
    /// Process and validate the AI response before returning it.
    /// Override this method to add custom response processing logic.
    /// </summary>
    protected virtual async Task<string> ProcessResponseAsync(TopicWorkflowContext context, object? input, string response)
    {
        await Task.CompletedTask; // Base implementation is async for consistency
        
        if (RequireJsonOutput)
        {
            // Validate JSON format
            try
            {
                JsonDocument.Parse(response);
                _semanticLogger.LogDebug("JSON response validation successful");
            }
            catch (JsonException ex)
            {
                _semanticLogger.LogWarning(ex, "Invalid JSON response received, attempting to clean up");
                // Attempt to extract JSON from response if it's wrapped in other text
                response = ExtractJsonFromResponse(response);
            }
        }

        return response;
    }

    /// <summary>
    /// Store semantic activity results in the conversation context for use by subsequent activities.
    /// Override this method to customize how results are stored.
    /// </summary>
    protected virtual async Task StoreResultsInContextAsync(TopicWorkflowContext context, string response)
    {
        await Task.CompletedTask; // Base implementation is async for consistency
        
        var contextKey = $"{Id}_Result";
        context.SetValue(contextKey, response);
        
        if (RequireJsonOutput)
        {
            // Also store parsed JSON for easy access
            try
            {
                var jsonDocument = JsonDocument.Parse(response);
                context.SetValue($"{contextKey}_Json", jsonDocument);
                _semanticLogger.LogDebug("Stored JSON result in context at key {ContextKey}_Json", contextKey);
            }
            catch (JsonException ex)
            {
                _semanticLogger.LogWarning(ex, "Failed to parse JSON for context storage");
            }
        }
        
        _semanticLogger.LogDebug("Stored semantic activity result in context at key {ContextKey}", contextKey);
    }

    /// <summary>
    /// Handle errors that occur during semantic activity execution.
    /// Override this method to provide custom error handling.
    /// </summary>
    protected virtual async Task<ActivityResult> HandleErrorAsync(TopicWorkflowContext context, object? input, Exception exception)
    {
        await Task.CompletedTask; // Base implementation is async for consistency
        
        var errorMessage = $"I apologize, but I encountered an error while processing your request. Please try again.";
        
        return ActivityResult.Continue(errorMessage);
    }

    /// <summary>
    /// Extract JSON content from a response that may contain additional text.
    /// </summary>
    private string ExtractJsonFromResponse(string response)
    {
        try
        {
            // Look for JSON object boundaries
            var startIndex = response.IndexOf('{');
            var endIndex = response.LastIndexOf('}');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                var jsonCandidate = response.Substring(startIndex, endIndex - startIndex + 1);
                JsonDocument.Parse(jsonCandidate); // Validate
                return jsonCandidate;
            }
            
            // Look for JSON array boundaries
            startIndex = response.IndexOf('[');
            endIndex = response.LastIndexOf(']');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                var jsonCandidate = response.Substring(startIndex, endIndex - startIndex + 1);
                JsonDocument.Parse(jsonCandidate); // Validate
                return jsonCandidate;
            }
        }
        catch (JsonException)
        {
            // If extraction fails, return original response
        }
        
        return response;
    }
}