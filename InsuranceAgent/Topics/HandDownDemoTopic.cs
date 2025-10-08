using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

namespace InsuranceAgent.Topics
{
    /// <summary>
    /// Demonstration topic showing hand-down/regain control with sub-topic calls.
    /// This topic calls a sub-topic and waits for it to complete before continuing.
    /// </summary>
    public class HandDownDemoTopic : TopicFlow
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
            // Step 1: Log start and setup
            Add(new SimpleActivity("start-demo", (ctx, input) =>
            {
                _logger.LogInformation("[HandDownDemoTopic] Starting hand-down/regain control demonstration");
                ctx.SetValue("DemoStartTime", DateTime.UtcNow);
                
                // Set some context variables for the demo
                _conversationContext.SetValue("Global_DemoMode", "HandDownDemo");
                _conversationContext.SetValue("Global_ParentTopic", "HandDownDemoTopic");
                _conversationContext.SetValue("Global_TestScenario", "NestedTopicChaining");
                
                return Task.FromResult<object?>("Demo started - preparing to call sub-topic");
            }));

            // Step 2: Call the BeneficiaryInfoDemoTopic and WAIT for completion
            Add(new TriggerTopicActivity(
                "call-beneficiary-subtopic", 
                "BeneficiaryInfoDemoTopic", 
                _logger, 
                waitForCompletion: true,  // KEY: Wait for completion instead of hand-off!
                _conversationContext));

            // Step 3: This will ONLY execute AFTER the sub-topic completes
            Add(new SimpleActivity("after-subtopic-resume", (ctx, input) =>
            {
                _logger.LogInformation("[HandDownDemoTopic] 🎉 SUCCESSFULLY RESUMED after sub-topic completion!");
                
                // Access completion data from the sub-topic
                var completionData = ctx.GetValue<object>("SubTopicCompletionData");
                var resumeData = ctx.GetValue<object>("ResumeData");
                
                _logger.LogInformation("[HandDownDemoTopic] Sub-topic completion data: {CompletionData}", completionData);
                _logger.LogInformation("[HandDownDemoTopic] Resume data: {ResumeData}", resumeData);
                
                // Demonstrate that we can continue processing in the main topic
                _conversationContext.SetValue("Global_DemoResult", "SubTopicCompleted");
                
                return Task.FromResult<object?>("Sub-topic completed successfully, main topic continuing");
            }));

            // Step 4: Additional processing to prove the flow works
            Add(new SimpleActivity("final-processing", (ctx, input) =>
            {
                _logger.LogInformation("[HandDownDemoTopic] Performing final processing steps");
                
                var startTime = ctx.GetValue<DateTime>("DemoStartTime");
                var duration = DateTime.UtcNow - startTime;
                var demoResult = _conversationContext.GetValue<string>("Global_DemoResult");
                
                _logger.LogInformation("[HandDownDemoTopic] Demo completed in {Duration}ms with result: {Result}", 
                    duration.TotalMilliseconds, demoResult);
                
                // Set final demo statistics
                _conversationContext.SetValue("Global_DemoDurationMs", duration.TotalMilliseconds);
                _conversationContext.SetValue("Global_DemoSuccess", true);
                
                return Task.FromResult<object?>("Final processing completed");
            }));

            // Step 5: Complete the topic properly using CompleteTopicActivity
            Add(new CompleteTopicActivity(
                "complete-demo",
                completionData: new { 
                    DemoCompleted = true, 
                    TopicName = "HandDownDemoTopic",
                    TestType = "NestedTopicChaining",
                    Success = true
                },
                completionMessage: "Hand-down/regain control demonstration completed successfully! 🚀",
                _logger,
                _conversationContext));
        }

        public override Task<float> CanHandleAsync(
            string message, 
            CancellationToken cancellationToken = default)
        {
            // Expanded keyword matching for demonstration
            var keywords = new[] { 
                "handdown", "hand-down", "demo", "nested", "subtopic", 
                "chaining", "test", "demonstrate", "show me", "example" 
            };
            var inputLower = message.ToLowerInvariant();
            
            foreach (var keyword in keywords)
            {
                if (inputLower.Contains(keyword))
                {
                    _logger.LogInformation("[HandDownDemoTopic] Matched keyword '{Keyword}' in input: {Input}", keyword, message);
                    return Task.FromResult(0.8f); // High confidence for demo requests
                }
            }

            // Special handling for explicit demo requests
            if (inputLower.Contains("topic") && (inputLower.Contains("demo") || inputLower.Contains("test")))
            {
                _logger.LogInformation("[HandDownDemoTopic] Matched topic demo pattern in input: {Input}", message);
                return Task.FromResult(0.9f); // Very high confidence
            }

            return Task.FromResult(0.0f); // Can't handle this input
        }
    }
}