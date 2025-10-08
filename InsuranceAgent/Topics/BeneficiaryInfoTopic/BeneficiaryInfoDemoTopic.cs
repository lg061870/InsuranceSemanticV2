using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics.BeneficiaryInfoTopic;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics {
    /// <summary>
    /// Demo topic for capturing beneficiary information.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class BeneficiaryInfoDemoTopic : TopicFlow {
        public const string ActivityId_ShowCard = "ShowBeneficiaryInfoCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_GlobalVariable = "PromoteBeneficiaryToGlobal";
        public const string ActivityId_Trigger = "TriggerCaliforniaResident";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "beneficiary info", "beneficiary", "add beneficiary", "update beneficiary"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<BeneficiaryInfoDemoTopic> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public BeneficiaryInfoDemoTopic(
            TopicWorkflowContext context,
            ILogger<BeneficiaryInfoDemoTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext,
            ILoggerFactory loggerFactory
        ) : base(context, logger, name: "BeneficiaryInfoDemoTopic") {
            _logger = logger;
            _conversationContext = conversationContext;
            _loggerFactory = loggerFactory;

            // Store topic metadata in conversation context
            _conversationContext.SetValue("BeneficiaryInfoDemoTopic_create", DateTime.UtcNow.ToString("o"));
            _conversationContext.SetValue("TopicName", "Beneficiary Info Demo");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "BeneficiaryInfoModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[BeneficiaryInfoCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                }
            );

            var isDevelopment =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            var dumpCtxActivity = new DumpCtxActivity(ActivityId_DumpCtx, isDevelopment);

            // GlobalVariableActivity to promote beneficiary data to conversation-global scope
            var globalVarActivity = new GlobalVariableActivity(
                ActivityId_GlobalVariable,
                _conversationContext,
                _loggerFactory.CreateLogger<GlobalVariableActivity>()
            );
            globalVarActivity.ShouldPromoteToGlobal = (key, value) => 
                key == "BeneficiaryInfoModel" || value is BeneficiaryInfoModel;

            var triggerActivity = new TriggerTopicActivity(
                ActivityId_Trigger,
                "CaliforniaResidentDemoTopic"
            );

            // === Event hooks for AdaptiveCard lifecycle ===
            showCardActivity.CardJsonEmitted += (s, e) =>
                _logger.LogInformation("[{Topic}] Card JSON emitted (mode={Mode})", Name, e.RenderMode);

            showCardActivity.CardJsonSending += (s, e) =>
                _logger.LogInformation("[{Topic}] Card JSON sending (mode={Mode})", Name, e.RenderMode);

            showCardActivity.CardJsonSent += (s, e) =>
                _logger.LogInformation("[{Topic}] Card JSON sent (mode={Mode})", Name, e.RenderMode);

            showCardActivity.CardJsonRendered += (s, e) =>
                _logger.LogInformation("[{Topic}] Card JSON rendered on client at {Time}", Name, e.RenderedAt);

            showCardActivity.CardDataReceived += (s, e) =>
                _logger.LogInformation("[{Topic}] Card data received: {Keys}", Name, string.Join(",", e.Data.Keys));

            showCardActivity.ModelBound += (s, e) => {
                _logger.LogInformation("[{Topic}] Model bound: {ModelType}", Name, e.Model?.GetType().Name);
                
                // Store in conversation context using structured model storage
                if (e.Model is BeneficiaryInfoModel beneficiaryModel) {
                    _conversationContext.SetModel(beneficiaryModel);
                    _logger.LogInformation("[{Topic}] Beneficiary model stored in conversation context", Name);
                }
            };

            showCardActivity.ValidationFailed += (s, e) =>
                _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

            // === Trigger hook ===
            triggerActivity.TopicTriggered += (sender, e) =>
            {
                _logger.LogInformation("[{Topic}] Triggering next topic: {Next}", Name, e.TopicName);
                _conversationContext.AddTopicToChain(e.TopicName);
            };

            // === Enqueue activities ===
            Add(showCardActivity);
            Add(dumpCtxActivity);
            Add(globalVarActivity);
            Add(triggerActivity);
        }


        /// <summary>
        /// Intent detection (basic keyword matching).
        /// </summary>
        public override Task<float> CanHandleAsync(
            string message,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
            var msg = message.ToLowerInvariant();

            foreach (var kw in IntentKeywords) {
                if (msg.Contains(kw)) return Task.FromResult(1.0f);
            }
            return Task.FromResult(0f);
        }

        /// <summary>
        /// Execute the topic’s activities in queue order.
        /// Also handles optional NextTopic context handoff.
        /// </summary>
        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
            _conversationContext.SetValue("BeneficiaryInfoDemoTopic_runasync", DateTime.UtcNow.ToString("o"));

            var task = base.RunAsync(cancellationToken);

            return task.ContinueWith(t => {
                var result = t.Result;

                // Check for next topic in conversation context
                var nextTopic = _conversationContext.GetValue<string>("NextTopic");
                if (!string.IsNullOrEmpty(nextTopic)) {
                    result.NextTopicName = nextTopic;
                    _conversationContext.SetValue("NextTopic", string.Empty); // reset
                }

                return result;
            });
        }
    }
}
