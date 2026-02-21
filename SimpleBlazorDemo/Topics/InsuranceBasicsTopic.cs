using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.TopicFlow.Extensions;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SimpleBlazorDemo.Services;
using SimpleBlazorDemo.Topics.Education;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Open-ended education topic for people who are new
/// to insurance and want plain-language basics.
/// </summary>
public class InsuranceBasicsTopic : TopicFlow {
    private readonly ILogger<InsuranceBasicsTopic> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Kernel _kernel;
    private readonly IVectorDatabaseService? _vectorDb;

    public InsuranceBasicsTopic(
        TopicWorkflowContext context,
        ILogger<InsuranceBasicsTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory,
        Kernel kernel,
        IVectorDatabaseService? vectorDb = null)
        : base(context, logger, "InsuranceBasicsTopic") {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _kernel = kernel;
        _vectorDb = vectorDb;
        BuildWorkflow();
    }

    private void BuildWorkflow() {
        // Mark basics mode active so routing prefers this topic
        Context.SetValue("InsuranceBasics_ModeActive", true);

        Add(new SimpleActivity(
            "BasicsIntro",
            "You’re in learning mode. I’ll stick to the basics and keep things in plain language. Ask me anything you’re curious about — like how life insurance works, what it’s for, or when people usually get it."
        ));
        // Track the last navigation label to drive the learning loop
        Context.SetValue("Basics_LastDecisionLabel", string.Empty);

        // Learning loop: repeat Q&A + empathy + decision while the
        // navigation label is StillLearning. Routing happens AFTER
        // this loop based on Basics_NextMode.
        Add(RepeatActivity.While<CompositeActivity>(
            "BasicsLearningLoop",
            (id, ctx) => {
                // 1) Ask for a basics question via the lightweight card
                var waitForQuestion = new WaitForUserInputActivity(
                    $"{id}_WaitForQuestion",
                    ctx,
                    _loggerFactory.CreateLogger<AdaptiveCardActivity<WaitForUserInputModel>>(),
                    "Ask your question about insurance basics:");

                // 2) Capture latest user question into context
                var captureQuestion = new SimpleActivity(
                    $"{id}_CaptureBasicsQuestion",
                    (c, input) => {
                        var lastUser = c.GetValue<string>("LastUserMessage") ?? string.Empty;
                        c.SetValue("Basics_UserPrompt", lastUser);
                        return Task.FromResult<object?>(null);
                    });

                // 3) Semantic intel extraction over free-form text
                var intelActivity = new SemanticResponseActivity(
                        id: $"{id}_Basics_Intel",
                        kernel: _kernel,
                        logger: _loggerFactory.CreateLogger<SemanticResponseActivity>(),
                        vectorDb: null,
                        collectionName: "insurance_basics_intel")
                    .WithDeveloperPrompt(GetBasicsIntelDeveloperPrompt())
                    .WithSkipLLMThreshold(0.0);

                intelActivity.UserPromptContextKey = "Basics_UserPrompt";
                intelActivity.RequireJsonOutput = true;
                intelActivity.WithDataIntelligence();

                                // 4) Answer using curated basics content (vector-backed)
                                var qaActivity = new SemanticResponseActivity(
                                                id: $"{id}_Basics_QA",
                                                kernel: _kernel,
                                                logger: _loggerFactory.CreateLogger<SemanticResponseActivity>(),
                                                vectorDb: _vectorDb,
                                                collectionName: LifeInsuranceBasicsEmbeddingService.CollectionName)
                                        .WithDeveloperPrompt(GetBasicsQaDeveloperPrompt())
                    .WithSkipLLMThreshold(0.9);

                qaActivity.UserPromptContextKey = "Basics_UserPrompt";

                // 5) Empathetic reflection reply (fresh prompt per iteration)
                var empathyPrompt = new PromptActivity(
                        $"{id}_BasicsEmpathyPrompt",
                        _kernel,
                        _loggerFactory.CreateLogger<PromptActivity>())
                    .WithSystemPrompt(GetBasicsEmpathySystemPrompt())
                    .WithUserPrompt(GetBasicsEmpathyUserPrompt());

                empathyPrompt.RequireJsonOutput = false;

                var empathyActivity = new SimpleActivity(
                    $"{id}_BasicsEmpathy",
                    async (c, input) => {
                        var result = await empathyPrompt.RunAsync(c, null, CancellationToken.None);
                        var reply = result.Message?.Trim();
                        if (string.IsNullOrWhiteSpace(reply)) {
                            reply = "Puedes contarme un poco más sobre tu situación y con gusto te explico las opciones básicas.";
                        }
                        return reply!;
                    });

                // 6) Decide next mode and record label (fresh prompt per iteration)
                var decision = new PromptActivity($"{id}_BasicsNextStepDecision", _kernel, _loggerFactory.CreateLogger<PromptActivity>())
                    .WithSystemPrompt(GetBasicsDecisionSystemPrompt())
                    .WithUserPrompt(GetBasicsDecisionUserPrompt());

                decision.RequireJsonOutput = false;

                var runDecision = new SimpleActivity(
                    $"{id}_RunBasicsDecision",
                    async (c, input) => {
                        var result = await decision.RunAsync(c, null, CancellationToken.None);
                        var raw = result.Message?.Trim() ?? string.Empty;
                        var label = raw;

                        if (!string.IsNullOrEmpty(label)) {
                            var norm = label.Replace("\"", string.Empty).Trim();
                            // Always remember the raw navigation label for loop control
                            c.SetValue("Basics_LastDecisionLabel", norm);

                            // Only set a routing label for concrete next modes
                            if (!norm.Equals("StillLearning", StringComparison.OrdinalIgnoreCase) &&
                                !norm.Equals("Done", StringComparison.OrdinalIgnoreCase)) {
                                c.SetValue("Basics_NextMode", norm);
                                label = norm;
                            }
                        }

                        return label;
                    });

                // 7) If the decision says "StillLearning", offer the user a
                // simple choice: keep asking free-form questions or let the
                // agent interview them with a structured flow.
                var modeChoice = ConditionalActivity<QuickAnswerActivity>.Switch(
                    $"{id}_BasicsModeChoice",
                    c => {
                        var last = c.GetValue<string>("Basics_LastDecisionLabel") ?? string.Empty;
                        return last.Equals("StillLearning", StringComparison.OrdinalIgnoreCase)
                            ? "ask"
                            : string.Empty;
                    },
                    new Dictionary<string, Func<string, TopicWorkflowContext, QuickAnswerActivity>> {
                        ["ask"] = (childId, childCtx) => new QuickAnswerActivity(
                            id: $"{id}_BasicsModeChoice",
                            question: "How would you like to continue?",
                            answers: new[] { "I need more info", "Interview me" },
                            context: childCtx,
                            logger: _loggerFactory.CreateLogger<QuickAnswerActivity>(),
                            isRequired: true)
                    },
                    defaultBranch: null,
                    logger: _loggerFactory.CreateLogger<ConditionalActivity<QuickAnswerActivity>>()
                );

                var applyModeChoice = new SimpleActivity(
                    $"{id}_ApplyBasicsModeChoice",
                    (c, input) => {
                        var last = c.GetValue<string>("Basics_LastDecisionLabel") ?? string.Empty;

                        // Only act when we were in StillLearning mode.
                        if (!last.Equals("StillLearning", StringComparison.OrdinalIgnoreCase))
                            return Task.FromResult<object?>(null);

                        var data = c.GetValue<Dictionary<string, object>>($"{id}_BasicsModeChoice");
                        if (data == null || !data.TryGetValue("answer", out var answerObj))
                            return Task.FromResult<object?>(null);

                        var answer = answerObj?.ToString() ?? string.Empty;

                        if (answer.Equals("Interview me", StringComparison.OrdinalIgnoreCase)) {
                            // User explicitly asked to be interviewed → move to coverage estimate
                            c.SetValue("Basics_NextMode", "CoverageEstimate");
                            c.SetValue("Basics_LastDecisionLabel", "CoverageEstimate");
                        } else {
                            // User wants more info → stay in learning loop
                            c.SetValue("Basics_LastDecisionLabel", "StillLearning");
                        }

                        return Task.FromResult<object?>(null);
                    });

                return CompositeActivity.Create(id,
                    waitForQuestion,
                    captureQuestion,
                    intelActivity,
                    qaActivity,
                    empathyActivity,
                    runDecision,
                    modeChoice,
                    applyModeChoice);
            },
            ctx => {
                // Continue looping while user is StillLearning.
                var label = ctx.GetValue<string>("Basics_LastDecisionLabel");
                if (string.IsNullOrWhiteSpace(label))
                    return true; // First iteration

                return label.Equals("StillLearning", StringComparison.OrdinalIgnoreCase);
            },
            logger: _loggerFactory.CreateLogger<RepeatActivity<CompositeActivity>>()
        ));

        // Branch based on Basics_NextMode. This will only be set when
        // the decision picked a concrete next mode (not StillLearning/Done).
        Add(ConditionalActivity<TriggerTopicActivity>.Switch(
            "BasicsNextStepRouting",
            ctx => {
                var raw = ctx.GetValue<string>("Basics_NextMode") ?? string.Empty;
                var key = raw.Trim().ToLowerInvariant();
                _logger.LogInformation("[InsuranceBasicsTopic] BasicsNextStepRouting selector chose key='{Key}' from Basics_NextMode='{Raw}'", key, raw);
                return key;
            },
            new Dictionary<string, Func<string, TopicWorkflowContext, TriggerTopicActivity>> {
                ["coverageestimate"] = (id, ctx) => {
                    _logger.LogInformation("[InsuranceBasicsTopic] Routing to CoverageEstimateTopic (activityId={ActivityId})", id);
                    return new TriggerTopicActivity(id, "CoverageEstimateTopic", _loggerFactory.CreateLogger<TriggerTopicActivity>());
                },
                ["comparetermvswhole"] = (id, ctx) => {
                    _logger.LogInformation("[InsuranceBasicsTopic] Routing to CompareTermVsWholeTopic (activityId={ActivityId})", id);
                    return new TriggerTopicActivity(id, "CompareTermVsWholeTopic", _loggerFactory.CreateLogger<TriggerTopicActivity>());
                },
                ["quote"] = (id, ctx) => {
                    _logger.LogInformation("[InsuranceBasicsTopic] Routing to QuoteIntakeTopic (activityId={ActivityId})", id);
                    return new TriggerTopicActivity(id, "QuoteIntakeTopic", _loggerFactory.CreateLogger<TriggerTopicActivity>());
                }
            },
            defaultBranch: null,
            logger: _loggerFactory.CreateLogger<ConditionalActivity<TriggerTopicActivity>>()
        ));
    }

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        _logger.LogInformation("[InsuranceBasicsTopic] RunAsync starting (State={State})", State);
        return base.RunAsync(cancellationToken);
    }
    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default) {
        // If basics mode is active in this conversation, prefer this topic
        // so follow-up questions stay in the Q&A loop.
        if (Context.GetValue<bool>("InsuranceBasics_ModeActive"))
            return Task.FromResult(0.95f);

        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(0.0f);

        // Entry keywords so routing can start the basics topic.
        if (input.Contains("insurance basics", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("teach me the basics", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("i'm new and want the basics", StringComparison.OrdinalIgnoreCase)) {
            return Task.FromResult(0.9f);
        }

        return Task.FromResult(0.0f);
    }

        // Prompt helpers (no internal double quotes in text)

        private static string GetBasicsIntelDeveloperPrompt() => @"You are an information extraction engine for an insurance lead.
Given the users free form message, which may be in Spanish or English,
extract any facts that seem relevant to an insurance conversation, such as:
- possible names
- number of children or dependents
- ages or age hints such as soy viejo or tengo 45 años
- main concern or confusion
- any other clearly useful details

Respond with JSON only as a flat object of key and value pairs.
For example, you can include keys like First Name, Dependents and Main Concern
with short, human readable values.

Use short, human readable keys. If you are not sure about a value,
either omit the key or set it to null. Do not include any text
outside the JSON object.";

        private static string GetBasicsQaDeveloperPrompt() => @"You are an insurance educator focused only on insurance basics.
Use the provided evidence from the basics document set to answer.
The user may write in Spanish or English; respond in the same language.

Stay strictly in education mode:
- plain language explanations
- friendly and empathetic tone that acknowledges that their situation is common
    and that it is normal to have doubts
- no sales push
- no legal, tax, or financial advice

If the question is outside basics, say so briefly and gently.
Keep answers concise, about two to four short paragraphs.";

                private static string GetBasicsEmpathySystemPrompt() => @"You are an empathetic insurance assistant.
The user may be writing in Spanish or English; always respond in the same
language they used.

Your job is to:
- acknowledge that their situation is common and that it is normal to have doubts
- carefully reflect back the key elements of what they shared, such as edad,
    hijos o dependientes, cirugías, cercanía al retiro or preocupaciones,
    without being overly repetitive
- look for implicit needs behind what they say; for example, an older
    person with two children may be worried about final expenses, leaving
    something for the children or making sure they are protected if something
    happens
- offer a short, plain language explanation of how insurance can generally
    help in situations like theirs, for example proteger a los hijos de una
    persona mayor, ayudar con gastos finales or planear para la etapa de retiro,
    without going into specific products
- ask one short, concrete follow up question aimed at understanding what
    they most want to solve now, such as whether they are more focused on
    protecting children, covering final expenses, replacing income from a job
    or leaving a small legacy
- end with a gentle call to action that gives them two options:
    one, allow you to ask a few quick and concrete questions such as edad
    aproximada, grandes deudas and a comfortable monthly budget so you can
    orient them toward a plan; or two, continue asking freely if they are
    not ready yet, including questions like que me recomendarias en mi caso
    or what would you recommend in my situation
- keep a warm, encouraging tone

Do not sell products or give legal or financial advice; stay at the level of
empathy and simple education.";

        private static string GetBasicsEmpathyUserPrompt() => @"User message: {context.Basics_UserPrompt}.

If the context already contains additional keys like Dependents,
Main Concern or similar, you may optionally mention them to show you paid
attention, but do not invent facts.";

        private static string GetBasicsDecisionSystemPrompt() => @"You are an insurance navigation assistant.
Given what the user has been asking about insurance basics and their light qualification answers,
decide the single best next mode from this list:
- CoverageEstimate
- CompareTermVsWhole
- Quote
- StillLearning
- Done

Rules:
- if the user seems confused or hesitant, prefer StillLearning
- if they are asking about how much coverage, lean toward CoverageEstimate
- if they are comparing term versus whole or products, choose CompareTermVsWhole
- if they explicitly want a quote or policy, choose Quote
- if they ask what you would recommend or clearly invite you to
    suggest a plan, for example with questions like what would you recommend,
    que me recomendarias or what should I do in my case, prefer
    CoverageEstimate unless they are explicitly asking for a quote
 - if they explicitly ask you to ask them questions about their
     situation (for example ""you ask me questions"", ""ask me more
     questions about my situation"", ""hazme preguntas"" or similar),
     treat this as an invitation to move into a structured
     question flow and prefer CoverageEstimate over StillLearning
- if they say they are done for now, choose Done

Respond only with the mode name and no extra text.";

        private static string GetBasicsDecisionUserPrompt() => @"User basics questions and answers are in context.
Light qualification answers are also in context.
Current basics user prompt: {context.Basics_UserPrompt}.

Decide the best next mode now.";
}
