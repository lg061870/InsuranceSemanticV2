using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

namespace ConversaCore.Topics
{
    /// <summary>
    /// Demonstration topic showing hand-down/regain control with sub-topic calls.
    /// This topic calls a sub-topic and waits for it to complete before continuing.
    /// </summary>
    public class HandDownDemoTopic : TopicFlow.TopicFlow
    {
        private readonly ILogger<HandDownDemoTopic> _logger;
        private readonly IConversationContext _conversationContext;

        public HandDownDemoTopic(
            TopicWorkflowContext context,
            ILogger<HandDownDemoTopic> logger,
            IConversationContext conversationContext)
            : base(context, logger, "HandDownDemoTopic")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _conversationContext = conversationContext ?? throw new ArgumentNullException(nameof(conversationContext));

            BuildActivityQueue();
        }

        private void BuildActivityQueue()
        {
            // Step 1: Log start
            Add(new SimpleActivity("start-demo", (ctx, input) =>
            {
                _logger.LogInformation("[HandDownDemoTopic] Starting hand-down/regain control demonstration");
                ctx.SetValue("DemoStartTime", DateTime.UtcNow);
                
                // Set some context variables manually for the demo
                _conversationContext.SetValue("Global_DemoMode", "HandDownDemo");
                _conversationContext.SetValue("Global_ParentTopic", "HandDownDemoTopic");
                
                return Task.FromResult<object?>("Demo started");
            }));

            // Step 2: Call a sub-topic and wait for completion
            Add(new TriggerTopicActivity(
                "call-beneficiary-subtopic", 
                "BeneficiaryInfoDemoTopic", 
                _logger, 
                waitForCompletion: true,  // This is the key - wait for completion!
                _conversationContext));

            // Step 3: This will only execute AFTER the sub-topic completes
            Add(new SimpleActivity("after-subtopic", (ctx, input) =>
            {
                _logger.LogInformation("[HandDownDemoTopic] Resumed after sub-topic completion!");
                
                // Check if we have completion data from the sub-topic
                var completionData = ctx.GetValue<object>("SubTopicCompletionData");
                var resumeData = ctx.GetValue<object>("ResumeData");
                
                _logger.LogInformation("[HandDownDemoTopic] Completion data: {CompletionData}", completionData);
                _logger.LogInformation("[HandDownDemoTopic] Resume data: {ResumeData}", resumeData);
                
                return Task.FromResult<object?>("Sub-topic completed, continuing main flow");
            }));

            // Step 4: Do some more work
            Add(new SimpleActivity("final-step", (ctx, input) =>
            {
                _logger.LogInformation("[HandDownDemoTopic] Performing final steps in main topic");
                
                var startTime = ctx.GetValue<DateTime>("DemoStartTime");
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("[HandDownDemoTopic] Total demo duration: {Duration}ms", duration.TotalMilliseconds);
                
                return Task.FromResult<object?>("Final steps completed");
            }));

            // Step 5: Complete the topic properly
            Add(new CompleteTopicActivity(
                "complete-demo",
                completionData: new { DemoCompleted = true, TopicName = "HandDownDemoTopic" },
                completionMessage: "Hand-down/regain control demonstration completed successfully",
                _logger,
                _conversationContext));
        }

        public override Task<float> CanHandleAsync(
            string message, 
            CancellationToken cancellationToken = default)
        {
            // Simple keyword matching for demonstration
            var keywords = new[] { "handdown", "hand-down", "demo", "nested", "subtopic" };
            var inputLower = message.ToLowerInvariant();
            
            foreach (var keyword in keywords)
            {
                if (inputLower.Contains(keyword))
                {
                    _logger.LogInformation("[HandDownDemoTopic] Matched keyword '{Keyword}' in input: {Input}", keyword, message);
                    return Task.FromResult(0.8f); // High confidence
                }
            }

            return Task.FromResult(0.0f); // Can't handle this input
        }
    }
}