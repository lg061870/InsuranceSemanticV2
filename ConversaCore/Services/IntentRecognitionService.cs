using ConversaCore.Topics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConversaCore.Services {
    public class IntentRecognitionService : IIntentRecognitionService {
        private readonly Kernel _kernel;
        private readonly TopicRegistry _topicRegistry;
        private readonly ILogger<IntentRecognitionService> _logger;

        public IntentRecognitionService(
            Kernel kernel,
            TopicRegistry topicRegistry,
            ILogger<IntentRecognitionService> logger) {
            _kernel = kernel;
            _topicRegistry = topicRegistry;
            _logger = logger;
        }

        public async Task<(string Intent, float Confidence)> RecognizeIntentAsync(
            string message,
            string? context = null) {
            try {
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

                var availableTopics = string.Join(", ",
                    _topicRegistry.GetAllTopics().Select(t => t.Name));

                var chatHistory = new ChatHistory(
                    $"You are an intent recognition system. " +
                    $"Analyze the user's message and determine which topic it relates to. " +
                    $"Available topics: {availableTopics}. " +
                    $"Return ONLY in the format 'TOPIC_NAME:CONFIDENCE' (example: 'InsurancePlans:0.78')."
                );

                if (!string.IsNullOrEmpty(context)) {
                    chatHistory.AddSystemMessage($"Context information: {context}");
                }

                chatHistory.AddUserMessage(message);

                var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory);
                var response = result.Content?.Trim() ?? "";

                _logger.LogDebug("Intent recognition raw response: {Response}", response);

                // Parse "TopicName:0.75"
                var parts = response.Split(':');
                if (parts.Length == 2 && float.TryParse(parts[1], out var confidence)) {
                    return (parts[0].Trim(), Math.Clamp(confidence, 0f, 1f));
                }

                _logger.LogWarning("Failed to parse intent response: {Response}", response);
                return ("Default", 0.4f);
            } catch (Exception ex) {
                _logger.LogError(ex, "Intent recognition failed for message: {Message}", message);
                return ("Default", 0.3f);
            }
        }

        public async Task<float> GetTopicConfidenceAsync(
            string message,
            string topicName,
            string? context = null) {
            try {
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

                var chatHistory = new ChatHistory(
                    $"You are a topic selection system. " +
                    $"Analyze the user's message and determine if it relates to the '{topicName}' topic. " +
                    $"Return ONLY a confidence score as a float between 0 and 1."
                );

                if (!string.IsNullOrEmpty(context)) {
                    chatHistory.AddSystemMessage($"Context information: {context}");
                }

                chatHistory.AddSystemMessage(GetTopicDescription(topicName));
                chatHistory.AddUserMessage(message);

                var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory);
                var response = result.Content?.Trim() ?? "";

                _logger.LogDebug("Confidence check raw response for {Topic}: {Response}", topicName, response);

                if (float.TryParse(response, out var confidence)) {
                    return Math.Clamp(confidence, 0f, 1f);
                }

                _logger.LogWarning("Could not parse confidence response: {Response}", response);
                return 0.3f;
            } catch (Exception ex) {
                _logger.LogError(ex, "Confidence check failed for topic {Topic}", topicName);
                return 0.3f;
            }
        }

        private string GetTopicDescription(string topicName) =>
            topicName switch {
                "Default" => "Handles greetings, chit-chat, and fallback responses.",
                "HealthQuestionnaire" => "Guides users through health-related questions.",
                "InsurancePlans" => "Explains different insurance plan options.",
                "Beneficiary" => "Manages adding or updating policy beneficiaries.",
                "ClaimsProcessing" => "Assists with filing and tracking claims.",
                "PolicyManagement" => "Handles updates, renewals, and cancellations.",
                "CustomerFeedback" => "Collects and responds to feedback.",
                _ => $"This is the {topicName} topic."
            };
    }
}
