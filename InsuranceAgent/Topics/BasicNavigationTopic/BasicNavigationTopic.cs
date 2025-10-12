using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.BasicNavigationTopic
{
    /// <summary>
    /// Basic navigation topic that shows users available options.
    /// Used as the default navigation option when compliance is not fully completed.
    /// </summary>
    public class BasicNavigationTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowBasicNavigationCard";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "navigate", "menu", "options", "help", "what can you do", 
            "choices", "available", "show me", "list"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<BasicNavigationTopic> _logger;

        public BasicNavigationTopic(
            TopicWorkflowContext context,
            ILogger<BasicNavigationTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "BasicNavigationTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("BasicNavigationTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Basic Navigation Menu");

            // Add a simple activity that sends a message
            Add(new SimpleActivity(ActivityId_ShowCard, async (ctx, data) => {
                _logger.LogInformation("[{Topic}] Showing basic navigation options", Name);
                
                // Create a message with navigation options
                var message = "Here are some things I can help you with:\n\n" +
                    "- Learn about insurance options\n" +
                    "- Compare different plans\n" +
                    "- Check coverage details\n" +
                    "- Find contact information\n\n" +
                    "What would you like to do?";
                
                // Store in context for UI to pick up
                ctx.SetValue("navigation_message", message);
                
                // Return the message
                return message;
            }));
        }
        
        /// <summary>
        /// Intent detection (keyword matching for basic navigation topics).
        /// </summary>
        public override Task<float> CanHandleAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
            var msg = message.ToLowerInvariant();

            var matchCount = 0;
            foreach (var kw in IntentKeywords)
            {
                if (msg.Contains(kw))
                {
                    matchCount++;
                }
            }

            // Calculate confidence based on keyword matches
            var confidence = matchCount > 0 ? Math.Min(0.8f, matchCount / 3.0f) : 0f;
            
            _logger.LogDebug("[{Topic}] Intent confidence: {Confidence} for message: {Message}", 
                Name, confidence, message);
                
            return Task.FromResult(confidence);
        }
    }
}