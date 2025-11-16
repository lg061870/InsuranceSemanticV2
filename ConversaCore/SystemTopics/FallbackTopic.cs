using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.SemanticKernel;
using ConversaCore.Interfaces;

namespace ConversaCore.SystemTopics {
    /// <summary>
    /// The FallbackTopic is activated when no other topic matches the user input.
    /// It uses SemanticResponseActivity to attempt an intelligent, evidence-based reply.
    /// Activities are initialized lazily at runtime to ensure proper scoped context.
    /// </summary>
    public sealed class FallbackTopic : TopicFlow.TopicFlow {
        public const string FallbackActivityId = "FallbackSemanticResponse";

        private readonly ILogger<FallbackTopic> _logger;
        private readonly Kernel _kernel;
        private readonly IVectorDatabaseService? _vectorDb;
        private bool _initialized;

        public FallbackTopic(
            TopicWorkflowContext context,
            ILogger<FallbackTopic> logger,
            Kernel kernel,
            IVectorDatabaseService? vectorDb = null)
            : base(context, logger) {
            _logger = logger;
            _kernel = kernel;
            _vectorDb = vectorDb;
        }

        public override string Name => "FallbackTopic";
        public override int Priority => int.MinValue; // Always last resort

        // ----------------------------------------------------------
        // Lazy initialization — executed once per scope
        // ----------------------------------------------------------
        private void InitializeFlow() {
            if (_initialized)
                return;

            _logger.LogInformation("[FallbackTopic] Initializing activities with Context #{Hash}", Context.GetHashCode());

            // 🧭 1️⃣ Developer baseline prompt
            string developerPrompt = @"
You are the system's fallback assistant.
Your goal is to help the user clarify their intent and guide them back to their original task.
Be concise and polite. If the message is unrelated, respond neutrally.
Never hallucinate facts about insurance, compliance, or legal matters.
If you believe the user was referring to an ongoing form or card, gently ask what part was unclear.
";

            // 🧠 2️⃣ Build Semantic Response dynamically with the current scoped context
            var semanticResponse = new SemanticResponseActivity(
                id: FallbackActivityId,
                kernel: _kernel,
                logger: _logger,
                vectorDb: _vectorDb,
                collectionName: "fallback_memory"
            )
            .WithDeveloperPrompt(developerPrompt)
            .WithPromptFactory(ctx => {
                // Capture the user’s last input from the *live scoped* context
                var userInput = ctx.GetValue<string>("Fallback_UserPrompt") ?? "(no user input)";
                return $"User said: '{userInput}'. Respond politely and ask for clarification if needed.";
            })
            .WithResponseHandler(async (ctx, response) => {
                ctx.SetValue("Fallback_LastResponse", response);
                _logger.LogInformation("[FallbackTopic] Stored semantic response: {Response}", response);
                await Task.CompletedTask;
            })
            .WithSkipLLMThreshold(0.92);

            // 💬 3️⃣ Optional echo confirmation activity
            var echo = new SimpleActivity("EchoResponse", async (ctx, _) => {
                var reply = ctx.GetValue<string>("Fallback_LastResponse") ?? "I'm not sure I understood.";
                _logger.LogInformation("[FallbackTopic] Echoing semantic reply: {Reply}", reply);
                await Task.Yield();
                return reply;
            });

            Add(semanticResponse);
            Add(echo);

            _initialized = true;
            _logger.LogInformation("[FallbackTopic] Activities initialized successfully.");
        }

        // ----------------------------------------------------------
        // Standard TopicFlow overrides
        // ----------------------------------------------------------
        public override async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
            _logger.LogInformation("[FallbackTopic] RunAsync starting (Context #{Hash})", Context.GetHashCode());
            InitializeFlow(); // 🔥 Build flow using live scoped context
            return await base.RunAsync(cancellationToken);
        }

        public override async Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
            await Task.CompletedTask;
            return 0.01f; // Always available, lowest priority
        }

        public override void Reset() {
            _logger.LogInformation("[FallbackTopic] Resetting fallback activities.");
            ClearActivities();
            _initialized = false; // allow re-init next run
        }
    }
}
