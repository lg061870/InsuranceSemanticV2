using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceLeadsAgent.Topics.SampleTopic.Cards;
using InsuranceLeadsAgent.Topics.SampleTopic.Models;
using Microsoft.Extensions.Logging;

using TopicFlowBase = ConversaCore.TopicFlow.TopicFlow;

namespace InsuranceLeadsAgent.Topics.SampleTopic
{
    /// <summary>
    /// Minimal sample TopicFlow demonstrating a simple welcome + card flow.
    /// This is generic and intended only as a starting point for domain developers.
    /// </summary>
    public partial class SampleTopic : TopicFlowBase
    {
        private readonly ILogger<SampleTopic> _logger;

        public SampleTopic(
            TopicWorkflowContext context,
            ILogger<SampleTopic> logger,
            IConversationContext conversationContext)
            : base(context, logger, "SampleTopic")
        {
            _logger = logger;
            BuildWorkflow();
        }

        public override void Reset()
        {
            base.Reset();
            BuildWorkflow();
        }

        private void BuildWorkflow()
        {
            Add(new SimpleActivity(
                "Welcome",
                "Welcome to the ConversaCore Blazor template. This is a sample topic you can replace with your own domain logic."));

            Add(DelayActivity.Create(
                "WelcomePause",
                TimeSpan.FromSeconds(1)));

            Add(new AdaptiveCardActivity<SampleInputCard, SampleInputModel>(
                "SampleInput",
                Context,
                card => card.Create(string.Empty)));

            Add(new SimpleActivity(
                "NextSteps",
                (ctx, input) =>
                {
                    var data = ctx.GetValue<Dictionary<string, object>>("SampleInput");
                    var message = data != null && data.TryGetValue("question", out var value)
                        ? value?.ToString() ?? string.Empty
                        : string.Empty;

                    var reply = string.IsNullOrWhiteSpace(message)
                        ? "Thanks for trying the sample topic. Use the Topic Tool to define your own flows and cards."
                        : $"You entered: '{message}'. In your own app, you would use this value to drive domain-specific logic.";

                    return Task.FromResult<object?>(reply);
                }));
        }

        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[SampleTopic] RunAsync starting (State={State})", State);
            return base.RunAsync(cancellationToken);
        }

        public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(input) &&
                input.Contains("sample topic", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(0.8f);
            }

            return Task.FromResult(0.0f);
        }
    }
}
