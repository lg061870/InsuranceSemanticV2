# InsuranceAgent Improvement Action Plan

This document provides a detailed action plan with specific tasks to address the issues identified in Chapter 8 of the SystemAudit_91825 report. Each task includes file references and specific implementation details.

## High Priority Tasks

### 1. Enhance Error Handling

1. **Add try-catch blocks in InsuranceTopics.cs activities:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Topics\InsuranceTopics.cs`
   - Wrap each activity lambda in BuildConversationStartTopic with try-catch blocks
   - Example implementation:
   ```csharp
   flow.AddSimpleActivity(
       "Initialize",
       async (activityContext) => 
       {
           try {
               logger.LogInformation("Initializing conversation variables");
               
               // Initialize lead summary
               var leadSummary = new InsuranceAgent.Models.LeadSummaryModel
               {
                   PersonalInfo = new InsuranceAgent.Models.PersonalInfoModel(),
                   Compliance = new InsuranceAgent.Models.ComplianceAndNotesModel(),
                   LifeGoals = new List<InsuranceAgent.Models.LifeGoalModel>()
               };
               
               context.SetValue("lead_summary", leadSummary);
               context.SetValue("consent_given", false);
               context.SetValue("goals_collected", false);
               
               return await Task.FromResult(ConversaCore.FlowBuilder.TopicActivityResult.CreateSuccessResult());
           }
           catch (Exception ex) {
               logger.LogError(ex, "Error in Initialize activity");
               context.SetValue("last_error", ex);
               context.SetValue("error_activity", "Initialize");
               return await Task.FromResult(ConversaCore.FlowBuilder.TopicActivityResult.CreateFailureResult(
                   "I'm experiencing a technical issue. Let me restart our conversation."));
           }
       },
       "Initialize conversation variables");
   ```

2. **Create a dedicated ErrorRecoveryTopic class:**
   - Create new file `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Topics\ErrorRecoveryTopic.cs`
   - Implement a topic that handles error recovery based on error type stored in context
   - Basic implementation structure:
   ```csharp
   using ConversaCore.Context;
   using ConversaCore.Topics;
   using Microsoft.Extensions.Logging;
   
   namespace InsuranceAgent.Topics
   {
       public class ErrorRecoveryTopic : TopicBase
       {
           private readonly ILogger<ErrorRecoveryTopic> _logger;
           private readonly IConversationContext _context;
           
           public ErrorRecoveryTopic(ILogger<ErrorRecoveryTopic> logger, IConversationContext context) 
               : base("ErrorRecovery", 900)  // High priority
           {
               _logger = logger;
               _context = context;
           }
           
           public override async Task<TopicResult> ProcessMessageAsync(
               string message, 
               CancellationToken cancellationToken = default)
           {
               var lastError = _context.GetValue<Exception>("last_error");
               var errorActivity = _context.GetValue<string>("error_activity");
               
               _logger.LogInformation("Error recovery for error in activity: {Activity}", errorActivity);
               
               // Clear error state
               _context.RemoveValue("last_error");
               _context.RemoveValue("error_activity");
               
               // Determine recovery path based on error activity
               string recoveryResponse;
               string nextTopic;
               
               switch (errorActivity)
               {
                   case "Initialize":
                       recoveryResponse = "I'm sorry, there was an issue starting our conversation. Let's try again.";
                       nextTopic = "ConversationStart";
                       break;
                       
                   case "CollectConsent":
                       recoveryResponse = "I apologize for the technical issue while collecting your consent. Let me try again.";
                       nextTopic = "ConversationStart";
                       break;
                       
                   // Add cases for other activities
                       
                   default:
                       recoveryResponse = "I apologize for the technical issue. Let's restart our conversation.";
                       nextTopic = "ConversationStart";
                       break;
               }
               
               return new TopicResult
               {
                   Response = recoveryResponse,
                   IsCompleted = true,
                   NextTopicName = nextTopic
               };
           }
       }
   }
   ```

3. **Register ErrorRecoveryTopic in Program.cs:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Program.cs`
   - Add registration for the new topic:
   ```csharp
   // --- InsuranceAgent-specific Topics ---
   builder.Services.AddScoped<ITopic, ErrorRecoveryTopic>();
   ```

4. **Improve error messages in HybridChatService.cs:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Services\HybridChatService.cs`
   - Enhance the catch block in ProcessMessageAsync with more user-friendly error messages:
   ```csharp
   catch (Exception ex) {
       _logger.LogError(ex, "Error processing message in hybrid service");
       
       // Categorize errors for better user feedback
       string errorMessage = "I'm sorry, I encountered an issue. ";
       
       if (ex is HttpRequestException)
           errorMessage += "I'm having trouble connecting to our services. Please try again in a moment.";
       else if (ex is TimeoutException)
           errorMessage += "Our service is taking too long to respond. Please try again shortly.";
       else if (ex is InvalidOperationException)
           errorMessage += "I couldn't complete that action. Let's try a different approach.";
       else
           errorMessage += "Please try rephrasing your question or starting a new topic.";
       
       return new ChatResponse {
           Content = errorMessage,
           IsAdaptiveCard = false,
           AdaptiveCardJson = null,
           Events = new List<ChatEvent>(),
           UsedTopicSystem = false
       };
   }
   ```

### 2. Improve Topic Confidence Scoring

1. **Enhance intent detection in InsuranceTopics.cs:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Topics\InsuranceTopics.cs`
   - Replace the simple string-based ExtractGoals method with a more sophisticated implementation:
   ```csharp
   private List<string> ExtractGoals(string message)
   {
       List<string> goals = new List<string>();
       var normalizedMessage = message.ToLower();
       
       // Define goal categories with weighted keywords
       Dictionary<string, (string[] keywords, double[] weights)> goalCategories = new()
       {
           ["Income Replacement"] = (
               new[] { "income", "salary", "job", "work", "earnings", "paycheck", "money", "wage", "revenue" },
               new[] { 1.0, 1.0, 0.8, 0.8, 0.9, 0.9, 0.7, 0.9, 0.8 }
           ),
           ["Mortgage Protection"] = (
               new[] { "mortgage", "house", "home", "property", "loan", "residence", "real estate", "homeowner" },
               new[] { 1.0, 0.8, 0.8, 0.7, 0.7, 0.8, 0.9, 0.9 }
           ),
           ["Children's Education"] = (
               new[] { "child", "children", "education", "college", "school", "university", "tuition", "student", "degree" },
               new[] { 0.9, 1.0, 1.0, 1.0, 0.8, 0.9, 0.9, 0.8, 0.8 }
           ),
           ["Peace of Mind"] = (
               new[] { "peace", "mind", "worry", "stress", "anxiety", "concern", "security", "stability", "protection" },
               new[] { 0.9, 0.9, 0.8, 0.8, 0.8, 0.7, 0.8, 0.7, 0.7 }
           ),
           ["Final Expenses"] = (
               new[] { "final", "funeral", "burial", "expenses", "death", "end of life", "cremation", "memorial" },
               new[] { 0.8, 1.0, 1.0, 0.8, 0.8, 0.9, 0.9, 0.9 }
           ),
       };
       
       // Track confidence scores for each category
       Dictionary<string, double> goalScores = new();
       
       // Calculate scores
       foreach (var category in goalCategories)
       {
           double score = 0;
           var keywords = category.Value.keywords;
           var weights = category.Value.weights;
           
           for (int i = 0; i < keywords.Length; i++)
           {
               if (normalizedMessage.Contains(keywords[i]))
               {
                   score += weights[i];
               }
           }
           
           // Only consider as a goal if confidence exceeds threshold
           if (score > 0.7)
           {
               goalScores[category.Key] = score;
           }
       }
       
       // Get goals by confidence (descending)
       goals = goalScores
           .OrderByDescending(g => g.Value)
           .Select(g => g.Key)
           .ToList();
       
       // Check for uncertainty indicators
       string[] uncertaintyPhrases = { "not sure", "unsure", "don't know", "uncertain", "maybe" };
       if (uncertaintyPhrases.Any(phrase => normalizedMessage.Contains(phrase)))
       {
           goals.Add("Not Sure");
       }
       
       // If no goals were extracted, add a generic one
       if (goals.Count == 0)
       {
           goals.Add("General Protection");
       }
       
       return goals;
   }
   ```

2. **Add configuration for confidence thresholds in appsettings.json:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\appsettings.json`
   - Add configuration section for topic confidence:
   ```json
   "TopicConfidence": {
     "MinimumConfidenceThreshold": 0.6,
     "DefaultTopicName": "Fallback",
     "ContextBoostFactor": 0.2
   }
   ```

3. **Make confidence thresholds configurable in HybridChatService.cs:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Services\HybridChatService.cs`
   - Replace hardcoded threshold with configuration:
   ```csharp
   private readonly IConfiguration _configuration;
   private readonly float _minimumConfidenceThreshold;
   
   public HybridChatService(
       ILogger<HybridChatService> logger,
       InsuranceAgentService agentService,
       ISemanticKernelService semanticKernelService,
       TopicRegistry topicRegistry,
       IConversationContext context,
       IConfiguration configuration) {
       _logger = logger;
       _agentService = agentService;
       _semanticKernelService = semanticKernelService;
       _topicRegistry = topicRegistry;
       _context = context;
       _configuration = configuration;
       
       // Get threshold from configuration or use default
       _minimumConfidenceThreshold = _configuration
           .GetSection("TopicConfidence")
           .GetValue<float>("MinimumConfidenceThreshold", 0.6f);
       
       _logger.LogInformation("HybridChatService initialized with confidence threshold: {Threshold:P2}",
           _minimumConfidenceThreshold);
   }
   ```

4. **Add context-aware confidence adjustment in TopicRegistry:**
   - Create a new extension method file `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Topics\TopicRegistryExtensions.cs`
   - Implement context-aware confidence adjustment:
   ```csharp
   using ConversaCore.Context;
   using ConversaCore.Topics;
   using Microsoft.Extensions.Configuration;
   
   namespace ConversaCore.Topics
   {
       public static class TopicRegistryExtensions
       {
           public static async Task<(ITopic topic, double confidence)> FindBestTopicWithContextAsync(
               this TopicRegistry registry,
               string message,
               IConversationContext context,
               IConfiguration configuration,
               CancellationToken cancellationToken = default)
           {
               // Get normal matching results
               var (topic, confidence) = await registry.FindBestTopicAsync(message, context, cancellationToken);
               
               // If no topic found, return immediately
               if (topic == null) return (null, 0);
               
               // Get boost factor from configuration
               var boostFactor = configuration
                   .GetSection("TopicConfidence")
                   .GetValue<double>("ContextBoostFactor", 0.2);
               
               // Apply context-based confidence adjustment
               var currentTopicName = context.CurrentTopic;
               if (!string.IsNullOrEmpty(currentTopicName) && topic.Name == currentTopicName)
               {
                   // Boost confidence for continuing the same topic
                   confidence = Math.Min(1.0, confidence + boostFactor);
               }
               
               return (topic, confidence);
           }
       }
   }
   ```

5. **Update HybridChatService to use context-aware topic matching:**
   - Modify ProcessMessageAsync method in `HybridChatService.cs`:
   ```csharp
   // Try topic matching with context awareness
   var (topic, confidence) = await _topicRegistry.FindBestTopicWithContextAsync(
       message, _context, _configuration, cancellationToken);
   _logger.LogInformation("Found topic {TopicName} with confidence {Confidence:P2}",
       topic?.Name ?? "null", confidence);
   ```

## Medium Priority Tasks

### 1. Optimize Hybrid Selection Algorithm

1. **Implement topic matching cache in HybridChatService.cs:**
   - Open `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Services\HybridChatService.cs`
   - Add caching for recent topic matches:
   ```csharp
   private readonly LruCache<string, (string TopicName, double Confidence)> _topicMatchCache = 
       new(capacity: 100);  // Adjust capacity as needed
   
   // Add a helper method for topic matching with cache
   private async Task<(ITopic topic, double confidence)> FindBestTopicWithCacheAsync(
       string message, 
       CancellationToken cancellationToken)
   {
       // Normalize message for cache key
       string cacheKey = message.ToLower().Trim();
       
       // Check cache first
       if (_topicMatchCache.TryGetValue(cacheKey, out var cachedResult))
       {
           _logger.LogInformation("Using cached topic match for message: {TopicName} ({Confidence:P2})", 
               cachedResult.TopicName, cachedResult.Confidence);
               
           if (cachedResult.TopicName == null)
               return (null, 0);
               
           var topic = _topicRegistry.GetTopic(cachedResult.TopicName);
           return (topic, cachedResult.Confidence);
       }
       
       // No cache hit, perform regular matching
       var (topic, confidence) = await _topicRegistry.FindBestTopicWithContextAsync(
           message, _context, _configuration, cancellationToken);
           
       // Cache the result
       _topicMatchCache.Add(cacheKey, (topic?.Name, confidence));
       
       return (topic, confidence);
   }
   ```

2. **Implement progressive topic matching in TopicRegistry:**
   - Create a new file `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Topics\ProgressiveTopicMatcher.cs`
   - Implement a progressive matching algorithm:
   ```csharp
   using ConversaCore.Context;
   using ConversaCore.Topics;
   
   namespace ConversaCore.Topics
   {
       public class ProgressiveTopicMatcher
       {
           private readonly TopicRegistry _registry;
           private readonly ILogger<ProgressiveTopicMatcher> _logger;
           
           public ProgressiveTopicMatcher(TopicRegistry registry, ILogger<ProgressiveTopicMatcher> logger)
           {
               _registry = registry;
               _logger = logger;
           }
           
           public async Task<(ITopic topic, double confidence)> MatchTopicProgressivelyAsync(
               string message,
               IConversationContext context,
               CancellationToken cancellationToken = default)
           {
               // Strategy 1: Check current topic first (most likely match)
               var currentTopicName = context.CurrentTopic;
               if (!string.IsNullOrEmpty(currentTopicName))
               {
                   var currentTopic = _registry.GetTopic(currentTopicName);
                   if (currentTopic != null)
                   {
                       var (canHandle, confidence) = await currentTopic.CanHandleAsync(message, context);
                       if (canHandle && confidence > 0.4) // Lower threshold for current topic
                       {
                           _logger.LogInformation("Current topic {TopicName} can handle the message with confidence {Confidence:P2}",
                               currentTopicName, confidence);
                           return (currentTopic, confidence + 0.1); // Boost confidence
                       }
                   }
               }
               
               // Strategy 2: Check topics in the chain
               var topicChain = context.TopicChain;
               foreach (var topicName in topicChain)
               {
                   var chainTopic = _registry.GetTopic(topicName);
                   if (chainTopic != null)
                   {
                       var (canHandle, confidence) = await chainTopic.CanHandleAsync(message, context);
                       if (canHandle && confidence > 0.5)
                       {
                           _logger.LogInformation("Chain topic {TopicName} can handle the message with confidence {Confidence:P2}",
                               topicName, confidence);
                           return (chainTopic, confidence);
                       }
                   }
               }
               
               // Strategy 3: Check high priority topics (likely system topics)
               var highPriorityTopics = _registry.GetAllTopics()
                   .Where(t => t.Priority > 500)
                   .OrderByDescending(t => t.Priority);
                   
               foreach (var topic in highPriorityTopics)
               {
                   var (canHandle, confidence) = await topic.CanHandleAsync(message, context);
                   if (canHandle && confidence > 0.6)
                   {
                       _logger.LogInformation("High priority topic {TopicName} can handle the message with confidence {Confidence:P2}",
                           topic.Name, confidence);
                       return (topic, confidence);
                   }
               }
               
               // Strategy 4: Full scan of remaining topics
               var remainingTopics = _registry.GetAllTopics()
                   .Where(t => t.Priority <= 500)
                   .OrderByDescending(t => t.Priority);
                   
               ITopic bestTopic = null;
               double bestConfidence = 0;
               
               foreach (var topic in remainingTopics)
               {
                   var (canHandle, confidence) = await topic.CanHandleAsync(message, context);
                   if (canHandle && confidence > bestConfidence)
                   {
                       bestTopic = topic;
                       bestConfidence = confidence;
                   }
               }
               
               if (bestTopic != null)
               {
                   _logger.LogInformation("Found topic {TopicName} in full scan with confidence {Confidence:P2}",
                       bestTopic.Name, bestConfidence);
               }
               
               return (bestTopic, bestConfidence);
           }
       }
   }
   ```

3. **Add telemetry for selection effectiveness in HybridChatService.cs:**
   - Add telemetry methods to track approach selection:
   ```csharp
   private readonly TelemetryClient _telemetry = new TelemetryClient();
   
   private void TrackSelectionTelemetry(string approachUsed, ITopic topic, double confidence, bool successful)
   {
       var telemetryProps = new Dictionary<string, string>
       {
           ["ApproachUsed"] = approachUsed,
           ["TopicName"] = topic?.Name ?? "None",
           ["Confidence"] = confidence.ToString("P2"),
           ["Successful"] = successful.ToString()
       };
       
       _telemetry.TrackEvent("ConversationApproachSelection", telemetryProps);
   }
   ```

4. **Use the telemetry in ProcessMessageAsync:**
   ```csharp
   if (topic != null && confidence >= _minimumConfidenceThreshold) {
       _logger.LogInformation("Using topic-based approach with {TopicName}", topic.Name);
       var result = await _agentService.ProcessMessageAsync(message, cancellationToken);
       TrackSelectionTelemetry("Topic", topic, confidence, true);
       return result;
   }
   
   // Fallback → use direct LLM
   _logger.LogInformation("No strong topic match, using direct LLM approach");
   var skResponse = await _semanticKernelService.ProcessMessageAsync(message, sessionState);
   
   TrackSelectionTelemetry("LLM", topic, confidence, false);
   ```

### 2. Enhance State Management

1. **Add schema validation for context variables:**
   - Create a new file `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Context\SchemaValidator.cs`
   - Implement basic schema validation:
   ```csharp
   using System.Collections.Generic;
   using System.Text.Json;
   
   namespace ConversaCore.Context
   {
       public static class SchemaValidator
       {
           private static readonly Dictionary<string, JsonDocument> _schemas = new();
           
           // Register schemas
           public static void RegisterSchema(string key, string jsonSchema)
           {
               _schemas[key] = JsonDocument.Parse(jsonSchema);
           }
           
           // Validate value against schema
           public static bool ValidateAgainstSchema<T>(string key, T value)
           {
               if (!_schemas.ContainsKey(key))
                   return true; // No schema registered, consider valid
                   
               try
               {
                   var valueJson = JsonSerializer.Serialize(value);
                   var valueDoc = JsonDocument.Parse(valueJson);
                   
                   // Basic schema validation logic here
                   // In a real implementation, use a proper JSON Schema validator
                   return true; // Placeholder
               }
               catch
               {
                   return false;
               }
           }
       }
   }
   ```

2. **Extend ConversationContext with validation:**
   - Create a new file `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Context\ValidatingConversationContext.cs`
   - Implement a decorating context with validation:
   ```csharp
   namespace ConversaCore.Context
   {
       public class ValidatingConversationContext : IConversationContext
       {
           private readonly IConversationContext _innerContext;
           private readonly ILogger<ValidatingConversationContext> _logger;
           
           public ValidatingConversationContext(
               IConversationContext innerContext,
               ILogger<ValidatingConversationContext> logger)
           {
               _innerContext = innerContext;
               _logger = logger;
           }
           
           // Implement interface with validation
           public string ConversationId => _innerContext.ConversationId;
           public string UserId => _innerContext.UserId;
           public string CurrentTopic => _innerContext.CurrentTopic;
           public IReadOnlyList<string> TopicChain => _innerContext.TopicChain;
           
           public T GetValue<T>(string key)
           {
               return _innerContext.GetValue<T>(key);
           }
           
           public void SetValue<T>(string key, T value)
           {
               if (!SchemaValidator.ValidateAgainstSchema(key, value))
               {
                   _logger.LogWarning("Value for key {Key} failed schema validation", key);
                   throw new ArgumentException($"Value for key {key} does not match registered schema");
               }
               
               _innerContext.SetValue(key, value);
           }
           
           public void SetCurrentTopic(string topicName) => _innerContext.SetCurrentTopic(topicName);
           public void AddTopicToChain(string topicName) => _innerContext.AddTopicToChain(topicName);
       }
   }
   ```

3. **Add context cleanup for completed topics:**
   - Modify `InsuranceTopics.cs` to include cleanup in the CompleteTopic activities:
   ```csharp
   flow.AddSimpleActivity(
       "CompleteTopic",
       async (activityContext) => 
       {
           logger.LogInformation("Completing ConversationStartTopic");
           
           // Cleanup temporary context variables
           CleanupTopicContext(context);
           
           // Complete topic and transition to InsuranceNeeds topic
           return await Task.FromResult(ConversaCore.FlowBuilder.TopicActivityResult.CreateCompletedWithTransitionResult(
               "Thank you for sharing your goals with us. I'll use this information to help find the best insurance options for you.",
               "InsuranceNeeds"));
       },
       "Complete the conversation start topic");
       
   // Add helper method for context cleanup
   private void CleanupTopicContext(IConversationContext context)
   {
       // Keep permanent data, remove temporary variables
       context.RemoveValue("temp_consent_attempts");
       context.RemoveValue("temp_goal_extraction");
       // Keep lead_summary as it's needed across topics
   }
   ```

4. **Extend IConversationContext with RemoveValue method:**
   - Modify `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Context\IConversationContext.cs`:
   ```csharp
   public interface IConversationContext
   {
       // Existing methods...
       
       // Add RemoveValue method
       void RemoveValue(string key);
   }
   ```
   
   - Implement RemoveValue in ConversationContext:
   ```csharp
   public void RemoveValue(string key)
   {
       if (_values.ContainsKey(key))
       {
           _values.Remove(key);
       }
   }
   ```

### 3. Improve Conversation Flows

1. **Add recovery paths for unexpected responses:**
   - Modify the CollectLifeGoals activity in InsuranceTopics.cs to handle unexpected responses:
   ```csharp
   flow.AddInteractiveActivity(
       "CollectLifeGoals",
       async (message, activityContext) => 
       {
           logger.LogInformation($"Processing life goals response: {message}");
           
           // Check if response is on-topic
           bool isRelevant = IsRelevantToLifeGoals(message);
           
           if (!isRelevant)
           {
               // Off-topic response - provide gentle redirect
               int attemptCount = context.GetValue<int>("life_goals_attempts") ?? 0;
               context.SetValue("life_goals_attempts", attemptCount + 1);
               
               if (attemptCount >= 2)
               {
                   // Too many attempts, move on with default goals
                   logger.LogInformation("Multiple off-topic responses, proceeding with default goals");
                   
                   var leadSummary = context.GetValue<InsuranceAgent.Models.LeadSummaryModel>("lead_summary");
                   leadSummary.LifeGoals.Add(new InsuranceAgent.Models.LifeGoalModel { 
                       GoalName = "General Protection", 
                       Priority = 3,
                       PlanningStatus = "Not Started"
                   });
                   
                   context.SetValue("lead_summary", leadSummary);
                   context.SetValue("goals_collected", true);
                   
                   return await Task.FromResult(ConversaCore.FlowBuilder.TopicActivityResult.CreateContinueWithMessageResult(
                       "I'll note down general protection as your goal. Let's move on to the next step."));
               }
               
               // Provide guidance and try again
               return await Task.FromResult(ConversaCore.FlowBuilder.TopicActivityResult.CreateWaitingForInputResult(
                   "I'd like to understand your insurance goals better. For example, are you interested in income replacement, " +
                   "mortgage protection, children's education, or something else?"));
           }
           
           // Extract goals from message (original logic)
           List<string> extractedGoals = ExtractGoals(message);
           
           // Rest of original implementation...
       },
       "Collect user's life insurance goals");
       
   // Helper method to check relevance
   private bool IsRelevantToLifeGoals(string message)
   {
       var normalizedMessage = message.ToLower();
       
       // Check for goal-related terms
       string[] goalTerms = { "goal", "protect", "coverage", "insurance", "need", "want", "income", "mortgage", 
           "education", "college", "funeral", "death", "family", "children", "spouse", "home", "house", "loan" };
           
       return goalTerms.Any(term => normalizedMessage.Contains(term));
   }
   ```

2. **Implement conversation summarization:**
   - Create a new file `c:\Users\lg061\source\repos\InsuranceSemantic\InsuranceAgent\Services\ConversationSummarizer.cs`
   - Implement basic summarization:
   ```csharp
   using ConversaCore.Models;
   using ConversaCore.Context;
   using Microsoft.Extensions.Logging;
   
   namespace InsuranceAgent.Services
   {
       public class ConversationSummarizer
       {
           private readonly ISemanticKernelService _semanticService;
           private readonly ILogger<ConversationSummarizer> _logger;
           
           public ConversationSummarizer(
               ISemanticKernelService semanticService,
               ILogger<ConversationSummarizer> logger)
           {
               _semanticService = semanticService;
               _logger = logger;
           }
           
           public async Task<string> SummarizeConversationAsync(
               List<ChatMessage> messages,
               IConversationContext context)
           {
               if (messages.Count < 3)
                   return "Conversation just started";
                   
               try
               {
                   // Format conversation for summarization
                   var conversationText = FormatConversationForSummary(messages);
                   
                   // Use Semantic Kernel for summarization
                   var prompt = $"Summarize this conversation briefly, focusing on key insurance needs and information shared:\n\n{conversationText}";
                   
                   var response = await _semanticService.GenerateResponseAsync(
                       prompt, context, CancellationToken.None);
                       
                   return response.Content;
               }
               catch (Exception ex)
               {
                   _logger.LogError(ex, "Error summarizing conversation");
                   return "Unable to summarize conversation";
               }
           }
           
           private string FormatConversationForSummary(List<ChatMessage> messages)
           {
               var formattedConversation = new StringBuilder();
               
               foreach (var message in messages.TakeLast(10)) // Last 10 messages
               {
                   var role = message.IsFromUser ? "User" : "Assistant";
                   formattedConversation.AppendLine($"{role}: {message.Content}");
               }
               
               return formattedConversation.ToString();
           }
       }
   }
   ```

3. **Enhance personalization based on context:**
   - Update InsuranceAgentService.cs to include personalization:
   ```csharp
   public async Task<ChatResponse> ProcessMessageAsync(
       string userMessage,
       CancellationToken cancellationToken = default)
   {
       _logger.LogInformation("Processing user message: {Message}", userMessage);
   
       var personalizedResponse = await PersonalizeResponseAsync(userMessage);
       if (personalizedResponse != null)
       {
           return personalizedResponse;
       }
   
       // Original implementation...
   }
   
   private async Task<ChatResponse> PersonalizeResponseAsync(string userMessage)
   {
       try
       {
           // Get personal info from context
           var leadSummary = _context.GetValue<InsuranceAgent.Models.LeadSummaryModel>("lead_summary");
           if (leadSummary == null || string.IsNullOrEmpty(leadSummary.Name))
               return null;
               
           // Check for greetings that should be personalized
           if (IsGreeting(userMessage))
           {
               string greeting = $"Hello {leadSummary.Name}! How can I assist you with your insurance needs today?";
               return new ChatResponse
               {
                   Content = greeting,
                   UsedTopicSystem = false
               };
           }
           
           // Check for returning after a break
           var lastInteraction = _context.GetValue<DateTime?>("last_interaction_time");
           var currentTime = DateTime.UtcNow;
           
           if (lastInteraction.HasValue && (currentTime - lastInteraction.Value).TotalMinutes > 30)
           {
               string welcomeBack = $"Welcome back, {leadSummary.Name}! I'm ready to continue helping you with your insurance needs.";
               return new ChatResponse
               {
                   Content = welcomeBack,
                   UsedTopicSystem = false
               };
           }
           
           // Update last interaction time
           _context.SetValue("last_interaction_time", currentTime);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Error in personalization");
       }
       
       return null;
   }
   
   private bool IsGreeting(string message)
   {
       var normalizedMessage = message.ToLower().Trim();
       return normalizedMessage == "hello" || normalizedMessage == "hi" || 
              normalizedMessage == "hey" || normalizedMessage == "greetings";
   }
   ```

## Low Priority Tasks

### 1. UI Enhancements

1. **Add message persistence in CustomChatWindow.razor:**
   - Add local storage service in `CustomChatWindow.razor.cs`:
   ```csharp
   @inject IJSRuntime JSRuntime
   
   // In @code block:
   private async Task SaveMessagesToStorage()
   {
       try
       {
           var serializedMessages = JsonSerializer.Serialize(Messages);
           await JSRuntime.InvokeVoidAsync("localStorage.setItem", "chatMessages", serializedMessages);
       }
       catch (Exception ex)
       {
           Console.WriteLine($"Error saving messages: {ex.Message}");
       }
   }
   
   private async Task LoadMessagesFromStorage()
   {
       try
       {
           var serializedMessages = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "chatMessages");
           
           if (!string.IsNullOrEmpty(serializedMessages))
           {
               Messages = JsonSerializer.Deserialize<List<ChatMessage>>(serializedMessages) ?? new List<ChatMessage>();
               StateHasChanged();
           }
       }
       catch (Exception ex)
       {
           Console.WriteLine($"Error loading messages: {ex.Message}");
       }
   }
   
   protected override async Task OnInitializedAsync()
   {
       await LoadMessagesFromStorage();
       await base.OnInitializedAsync();
   }
   
   // Update SendMessage to save messages after adding new ones:
   private async Task SendMessage()
   {
       // Existing code...
       
       // After adding messages:
       await SaveMessagesToStorage();
   }
   ```

2. **Improve accessibility features:**
   - Update `CustomChatWindow.razor`:
   ```html
   <div class="chat-window-container" role="region" aria-label="Chat conversation">
       <div class="chat-header">
           <div class="chat-header-avatar">
               <div class="avatar-circle">
                   <img src="https://storage.googleapis.com/uxpilot-auth.appspot.com/avatars/avatar-ai-1.jpg" alt="Sofia" />
                   <div class="avatar-status" aria-hidden="true"></div>
               </div>
           </div>
           <div class="chat-header-info">
               <h3 class="chat-header-name" id="chat-with-name">Sofia</h3>
               <div class="chat-header-status">
                   <div class="status-indicator" aria-hidden="true"></div>
                   <span aria-labelledby="chat-with-name">AI Assistant • Online</span>
               </div>
           </div>
           <div class="chat-header-actions">
               <button class="chat-header-button" title="Clear chat" aria-label="Clear chat history" @onclick="ClearChat">
                   <i class="fa-solid fa-trash-can" aria-hidden="true"></i>
               </button>
           </div>
       </div>
   
       <div class="messages-container" @ref="messagesContainerRef" role="log" aria-live="polite">
           <!-- Rest of the implementation... -->
       </div>
   
       <div class="chat-suggestions" role="group" aria-label="Suggested responses">
           @foreach (var suggestion in CurrentSuggestions) {
               <div class="suggestion-chip" role="button" tabindex="0" @onclick="() => UseSuggestion(suggestion)" @onkeydown="@(e => { if (e.Key == "Enter") UseSuggestion(suggestion); })">
                   @suggestion
               </div>
           }
       </div>
   
       <div class="message-input-container">
           <label for="message-input" class="sr-only">Type your message</label>
           <textarea id="message-input"
                     @bind="CurrentMessage"
                     @bind:event="oninput"
                     @onkeydown="HandleKeyDown"
                     @onkeydown:preventDefault="@(true)"
                     placeholder="Type your message..."
                     disabled="@IsProcessing"
                     class="message-input"
                     rows="1"
                     aria-label="Message input"
                     @ref="messageInputRef"></textarea>
           <button class="send-button" 
                   @onclick="SendMessage" 
                   disabled="@(IsProcessing || string.IsNullOrWhiteSpace(CurrentMessage))"
                   aria-label="Send message">
               <i class="fa-solid fa-paper-plane" aria-hidden="true"></i>
           </button>
       </div>
   </div>
   
   <style>
       .sr-only {
           position: absolute;
           width: 1px;
           height: 1px;
           padding: 0;
           margin: -1px;
           overflow: hidden;
           clip: rect(0, 0, 0, 0);
           white-space: nowrap;
           border: 0;
       }
   </style>
   ```

3. **Enhance visual differentiation for response types:**
   - Update message rendering in `CustomChatWindow.razor`:
   ```html
   @if (message.IsFromUser) {
       <div class="message-group">
           <div class="message user-message">
               <div class="message-content">@message.Content</div>
               <div class="message-time">@message.Timestamp.ToString("h:mm tt")</div>
               <div class="message-actions">
                   <button class="message-action-button" title="Edit" @onclick="() => EditMessage(message)">
                       <i class="fa-solid fa-pen"></i>
                   </button>
               </div>
           </div>
       </div>
   }
   else {
       <div class="message-group">
           <div class="message bot-message @GetMessageTypeClass(message)">
               @if (message.IsAdaptiveCard && !string.IsNullOrEmpty(message.AdaptiveCardJson)) {
                   <div class="adaptive-card-container">
                       <AdaptiveCardRenderer CardJson="@message.AdaptiveCardJson"
                                             CardContainerId="@($"card-{message.Timestamp.Ticks}")"
                                             OnSubmit="HandleCardSubmit"
                                             OnAction="HandleCardAction" />
                   </div>
                   <div class="message-time">@message.Timestamp.ToString("h:mm tt")</div>
               }
               else {
                   <div class="message-content">@((MarkupString)FormatMessageContent(message.Content))</div>
                   <div class="message-time">@message.Timestamp.ToString("h:mm tt")</div>
                   <div class="message-actions">
                       <button class="message-action-button" title="Copy" @onclick="() => CopyMessageToClipboard(message)">
                           <i class="fa-solid fa-copy"></i>
                       </button>
                   </div>
               }
           </div>
       </div>
   }
   
   @code {
       // Add method to determine message type
       private string GetMessageTypeClass(ChatMessage message)
       {
           if (message.IsAdaptiveCard)
               return "card-message";
               
           if (message.Content.Contains("I'm sorry") || 
               message.Content.Contains("I apologize") ||
               message.Content.Contains("error"))
               return "error-message";
               
           if (message.Content.Contains("[IMPORTANT]") || 
               message.Content.Contains("important") ||
               message.Content.Contains("required"))
               return "important-message";
               
           if (message.Content.StartsWith("Thank you") || 
               message.Content.StartsWith("Great") || 
               message.Content.StartsWith("Excellent"))
               return "success-message";
               
           return "standard-message";
       }
   }
   ```

4. **Add CSS for new message types:**
   - Create or update CSS file for CustomChatWindow:
   ```css
   /* Standard message */
   .bot-message.standard-message {
       background-color: #f0f4f8;
   }
   
   /* Card message */
   .bot-message.card-message {
       background-color: #ffffff;
       border: 1px solid #e0e0e0;
       box-shadow: 0 2px 4px rgba(0,0,0,0.1);
   }
   
   /* Error message */
   .bot-message.error-message {
       background-color: #fff8f8;
       border-left: 4px solid #e74c3c;
   }
   
   /* Important message */
   .bot-message.important-message {
       background-color: #fff8f0;
       border-left: 4px solid #f39c12;
   }
   
   /* Success message */
   .bot-message.success-message {
       background-color: #f0fff8;
       border-left: 4px solid #2ecc71;
   }
   ```

### 2. Add Analytics

1. **Track topic usage in TopicRegistry:**
   - Create a new file `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Analytics\TopicAnalytics.cs`
   - Implement basic analytics tracking:
   ```csharp
   using ConversaCore.Topics;
   using Microsoft.Extensions.Logging;
   
   namespace ConversaCore.Analytics
   {
       public class TopicAnalytics
       {
           private readonly ILogger<TopicAnalytics> _logger;
           private readonly Dictionary<string, TopicStats> _topicStats = new();
           
           public TopicAnalytics(ILogger<TopicAnalytics> logger)
           {
               _logger = logger;
           }
           
           public void TrackTopicSelection(string topicName, double confidence, bool wasSuccessful)
           {
               if (!_topicStats.ContainsKey(topicName))
               {
                   _topicStats[topicName] = new TopicStats();
               }
               
               _topicStats[topicName].SelectionCount++;
               _topicStats[topicName].TotalConfidence += confidence;
               
               if (wasSuccessful)
               {
                   _topicStats[topicName].SuccessCount++;
               }
               
               _logger.LogInformation(
                   "Topic {TopicName} selected (success: {WasSuccessful}) - " +
                   "Total: {SelectionCount}, Success Rate: {SuccessRate:P2}",
                   topicName,
                   wasSuccessful,
                   _topicStats[topicName].SelectionCount,
                   _topicStats[topicName].SuccessRate);
           }
           
           public Dictionary<string, TopicAnalyticsResult> GetTopicAnalytics()
           {
               var results = new Dictionary<string, TopicAnalyticsResult>();
               
               foreach (var entry in _topicStats)
               {
                   results[entry.Key] = new TopicAnalyticsResult
                   {
                       TopicName = entry.Key,
                       SelectionCount = entry.Value.SelectionCount,
                       SuccessCount = entry.Value.SuccessCount,
                       SuccessRate = entry.Value.SuccessRate,
                       AverageConfidence = entry.Value.AverageConfidence
                   };
               }
               
               return results;
           }
           
           private class TopicStats
           {
               public int SelectionCount { get; set; }
               public int SuccessCount { get; set; }
               public double TotalConfidence { get; set; }
               
               public double SuccessRate => SelectionCount == 0 ? 0 : (double)SuccessCount / SelectionCount;
               public double AverageConfidence => SelectionCount == 0 ? 0 : TotalConfidence / SelectionCount;
           }
       }
       
       public class TopicAnalyticsResult
       {
           public string TopicName { get; set; }
           public int SelectionCount { get; set; }
           public int SuccessCount { get; set; }
           public double SuccessRate { get; set; }
           public double AverageConfidence { get; set; }
       }
   }
   ```

2. **Track conversation completion rates:**
   - Create a new file `c:\Users\lg061\source\repos\InsuranceSemantic\ConversaCore\Analytics\ConversationAnalytics.cs`
   - Implement conversation tracking:
   ```csharp
   using ConversaCore.Events;
   using Microsoft.Extensions.Logging;
   
   namespace ConversaCore.Analytics
   {
       public class ConversationAnalytics
       {
           private readonly ILogger<ConversationAnalytics> _logger;
           private readonly Dictionary<string, ConversationSession> _sessions = new();
           
           public ConversationAnalytics(ILogger<ConversationAnalytics> logger)
           {
               _logger = logger;
               
               // Subscribe to relevant events
               var eventBus = TopicEventBus.Instance;
               eventBus.Subscribe<TopicEvent>((evt) => ProcessEvent(evt));
           }
           
           private void ProcessEvent(TopicEvent evt)
           {
               if (string.IsNullOrEmpty(evt.ConversationId)) return;
               
               if (!_sessions.ContainsKey(evt.ConversationId))
               {
                   _sessions[evt.ConversationId] = new ConversationSession
                   {
                       ConversationId = evt.ConversationId,
                       StartTime = DateTime.UtcNow,
                       State = ConversationState.Active
                   };
               }
               
               var session = _sessions[evt.ConversationId];
               
               // Update session based on event type
               switch (evt.EventType)
               {
                   case TopicEventType.UserMessageReceived:
                       session.MessageCount++;
                       session.LastActivity = DateTime.UtcNow;
                       break;
                       
                   case TopicEventType.TopicCompleted:
                       session.CompletedTopics.Add(evt.TopicName);
                       break;
                       
                   case TopicEventType.ConversationCompleted:
                       session.State = ConversationState.Completed;
                       session.EndTime = DateTime.UtcNow;
                       LogSessionCompletion(session);
                       break;
                       
                   case TopicEventType.ConversationError:
                       session.ErrorCount++;
                       break;
               }
           }
           
           private void LogSessionCompletion(ConversationSession session)
           {
               _logger.LogInformation(
                   "Conversation {ConversationId} completed - " +
                   "Duration: {Duration:mm\\:ss}, Messages: {MessageCount}, Topics: {TopicCount}, Errors: {ErrorCount}",
                   session.ConversationId,
                   session.Duration,
                   session.MessageCount,
                   session.CompletedTopics.Count,
                   session.ErrorCount);
           }
           
           // Expose analytics data
           public Dictionary<string, ConversationAnalyticsResult> GetCompletedConversations()
           {
               var results = new Dictionary<string, ConversationAnalyticsResult>();
               
               foreach (var session in _sessions.Values.Where(s => s.State == ConversationState.Completed))
               {
                   results[session.ConversationId] = new ConversationAnalyticsResult
                   {
                       ConversationId = session.ConversationId,
                       DurationSeconds = session.Duration.TotalSeconds,
                       MessageCount = session.MessageCount,
                       CompletedTopicCount = session.CompletedTopics.Count,
                       ErrorCount = session.ErrorCount,
                       CompletionPathString = string.Join(" → ", session.CompletedTopics)
                   };
               }
               
               return results;
           }
           
           private enum ConversationState
           {
               Active,
               Completed,
               Abandoned
           }
           
           private class ConversationSession
           {
               public string ConversationId { get; set; }
               public DateTime StartTime { get; set; }
               public DateTime? EndTime { get; set; }
               public DateTime LastActivity { get; set; }
               public ConversationState State { get; set; }
               public int MessageCount { get; set; }
               public int ErrorCount { get; set; }
               public List<string> CompletedTopics { get; set; } = new List<string>();
               
               public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
           }
       }
       
       public class ConversationAnalyticsResult
       {
           public string ConversationId { get; set; }
           public double DurationSeconds { get; set; }
           public int MessageCount { get; set; }
           public int CompletedTopicCount { get; set; }
           public int ErrorCount { get; set; }
           public string CompletionPathString { get; set; }
       }
   }
   ```

3. **Identify common failure points:**
   - Add logic in ErrorRecoveryTopic to track failure points:
   ```csharp
   private readonly Dictionary<string, int> _failurePoints = new();
   
   public override async Task<TopicResult> ProcessMessageAsync(
       string message, 
       CancellationToken cancellationToken = default)
   {
       var errorActivity = _context.GetValue<string>("error_activity");
       
       // Track failure point
       if (!string.IsNullOrEmpty(errorActivity))
       {
           if (!_failurePoints.ContainsKey(errorActivity))
               _failurePoints[errorActivity] = 0;
               
           _failurePoints[errorActivity]++;
           
           // Log top failure points periodically
           if (_failurePoints.Values.Sum() % 10 == 0)
           {
               var topFailures = _failurePoints
                   .OrderByDescending(kv => kv.Value)
                   .Take(3)
                   .Select(kv => $"{kv.Key}: {kv.Value}")
                   .ToList();
                   
               _logger.LogWarning("Top failure points: {FailurePoints}", string.Join(", ", topFailures));
           }
       }
       
       // Rest of implementation...
   }
   ```

This document provides a comprehensive action plan with specific tasks to address the issues identified in Chapter 8 of the SystemAudit_91825 report. Each task includes file references and specific implementation details to guide the improvement process.