using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace InsuranceLeadsAgent.Topics
{
    public class TransferTopic : TopicFlow
    {
        private readonly ILogger<TransferTopic> _logger;

        public TransferTopic(
            TopicWorkflowContext context,
            ILogger<TransferTopic> logger)
            : base(context, logger, "Transfer")
        {
            _logger = logger;
            _logger.LogInformation("[TransferTopic] Constructor called, calling BuildWorkflow()");
            BuildWorkflow();
            _logger.LogInformation("[TransferTopic] Constructor complete");
        }

        public override void Reset()
        {
            _logger.LogInformation("[TransferTopic] Reset() called - clearing activities");
            base.Reset();
            _logger.LogInformation("[TransferTopic] base.Reset() complete, calling BuildWorkflow()");
            BuildWorkflow();
            _logger.LogInformation("[TransferTopic] BuildWorkflow() complete");
        }

        private void BuildWorkflow()
        {
            _logger.LogInformation("[TransferTopic] BuildWorkflow START");
            
            try
            {
                _logger.LogInformation("[TransferTopic] Adding AttemptLiveTransfer activity");
                Add(new SimpleActivity(
                    "AttemptLiveTransfer",
                    (ctx, _) =>
                    {
                        // Simulate event trigger for live transfer attempt
                        ctx.SetValue("transfer_attempted", true);
                        ctx.SetValue("transfer_event", new { eventName = "attempt_live_transfer", timestamp = DateTime.UtcNow });
                        return Task.FromResult<object?>("Transfer event triggered.");
                    }
                ));
                _logger.LogInformation("[TransferTopic] Added AttemptLiveTransfer");

                _logger.LogInformation("[TransferTopic] Adding SimulateTransferResult activity");
                Add(new SimpleActivity(
                    "SimulateTransferResult",
                    (ctx, _) =>
                    {
                        ctx.SetValue("transfer_successful", false);
                        return Task.FromResult<object?>("Transfer attempted.");
                    }
                ));
                _logger.LogInformation("[TransferTopic] Added SimulateTransferResult");

                _logger.LogInformation("[TransferTopic] Adding CompleteTopicActivity");
                Add(new CompleteTopicActivity("TransferComplete"));
                _logger.LogInformation("[TransferTopic] Added CompleteTopicActivity");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TransferTopic] Exception during BuildWorkflow()");
                throw;
            }
            
            _logger.LogInformation("[TransferTopic] BuildWorkflow END - total activities added: 3");
        }
    }
}
