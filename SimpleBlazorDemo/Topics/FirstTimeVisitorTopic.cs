using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SimpleBlazorDemo.Cards;
using SimpleBlazorDemo.Models;
using SimpleBlazorDemo.Services;
using SimpleBlazorDemo.Topics.Education;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Topic for first-time visitors (Persona #1), guiding through value check, trust, contact, consents, and initial guidance.
/// Follows the compliant flow with early exits and AI/human options.
/// </summary>
public class FirstTimeVisitorTopic : TopicFlow {
    private readonly ILoggerFactory _loggerFactory;
    private readonly Kernel _kernel;
    private readonly IVectorDatabaseService? _vectorDb;

    public FirstTimeVisitorTopic(
        TopicWorkflowContext context,
        ILogger<FirstTimeVisitorTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory,
        Kernel kernel,
        IVectorDatabaseService? vectorDb = null)
        : base(context, logger, "FirstTimeVisitorTopic") {
        _loggerFactory = loggerFactory;
        _kernel = kernel;
        _vectorDb = vectorDb;
        BuildWorkflow();
    }

    public override void Reset()
    {
        base.Reset();
        BuildWorkflow();
    }

    private void BuildWorkflow() {
        // ─────────────────────────────────────────────
        // VALUE PROPOSITION → TRUST / EDUCATION → CONTACT
        // ─────────────────────────────────────────────
        Add(new QuickAnswerActivity(
            "ValueCheck",
            "We help people protect their families with affordable life insurance. We compare options from trusted carriers and, in just a few guided questions, estimate how much coverage you might need and what it could cost. Does this sound like the kind of help you're looking for today?",
            new[]
            {
                "Yes, this is what I need",
                "I'm not sure yet, I just want to learn",
                "No, not for me"
            },
            Context,
            _loggerFactory.CreateLogger<QuickAnswerActivity>()
        ));

        // If user continues, show a trust message in chat.
        // Warm lead: user explicitly wants help → trust message.
        Add(FlowConditionHelpers.IfCase(
            "ValueTrustMessage",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new SimpleActivity(
                "TrustSignalsMessage",
                "We are a licensed, A-rated carrier working with trusted partners. Your information is protected and used only to help match you with the right coverage. Please help us with your contact information to get started."
            )
        ));

        // Cold / unsure lead: focus on education first.
        Add(FlowConditionHelpers.IfCase(
            "EduIntroGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "I'm not sure yet, I just want to learn",
            new SimpleActivity(
                "EduIntroMessage",
                "No problem — many people start by just getting clear, unbiased information. I can walk you through how life insurance works and common questions so you can decide if it’s worth it for you."
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "EduNextStepGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "I'm not sure yet, I just want to learn",
            new QuickAnswerActivity(
                "EduNextStep",
                "What would you like to do next?",
                new[]
                {
                    "Give me a quick overview",
                    "I’ll just browse content"
                },
                Context,
                _loggerFactory.CreateLogger<QuickAnswerActivity>()
            )
        ));

        // If they want an overview, give a short educational explanation then
        // let the flow continue into the applicability/guidance steps.
        // If they want an overview, first acknowledge and give a "working" hint,
        // then run the semantic life-insurance-basics module.
        Add(FlowConditionHelpers.IfCase(
            "EduOverviewPrepGate",
            ctx => ctx.GetModelProperty<string>("EduNextStep", "answer", string.Empty) == "Give me a quick overview",
            new SimpleActivity(
                "EduOverviewPrepMessage",
                "Got it — let me pull together a short overview based on our learning library. This may take a few seconds."
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "EduOverviewGate",
            ctx => ctx.GetModelProperty<string>("EduNextStep", "answer", string.Empty) == "Give me a quick overview",
            LifeInsuranceBasicsModule.CreateActivity(
                _kernel,
                _loggerFactory.CreateLogger<SemanticResponseActivity>(),
                _vectorDb
            )
        ));

        // If they prefer to just browse, exit gracefully.
        Add(FlowConditionHelpers.IfCase(
            "EduBrowseExitMessageGate",
            ctx => ctx.GetModelProperty<string>("EduNextStep", "answer", string.Empty) == "I’ll just browse content",
            new SimpleActivity(
                "EduBrowseExitMessage",
                "Totally fine. I’ll stop asking questions — you can scroll through guides and FAQs, and you’re welcome to come back here if you want a personalized walkthrough later."
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "EduBrowseSuggestionsGate",
            ctx => ctx.GetModelProperty<string>("EduNextStep", "answer", string.Empty) == "I’ll just browse content",
            new ShowSuggestionsActivity(
                "EduBrowseSuggestions",
                new[]
                {
                    "Coverage guides",
                    "FAQs",
                    "Life insurance basics"
                }
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "EduBrowseExitGate",
            ctx => ctx.GetModelProperty<string>("EduNextStep", "answer", string.Empty) == "I’ll just browse content",
            new EndActivity("EduBrowseExit")
        ));

        // If user does not see a fit at all, show a final message and then end.
        Add(FlowConditionHelpers.IfCase(
            "ValueExitMessageGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "No, not for me",
            new SimpleActivity(
                "ValueExitMessage",
                "Thanks for visiting. We’ll send you a short message with a reference number you can mention if you’d like to pick this up again or have an agent reach out directly."
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "ValueExitGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "No, not for me",
            new EndActivity("ValueExit")
        ));

        // Only ask for contact info if the user clearly wants help now.
        Add(FlowConditionHelpers.IfCase(
            "ContactCaptureGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new AdaptiveCardActivity<ContactCaptureCard, ContactModel>(
                "ContactCapture",
                Context,
                c => c.Create()
            )
        ));

        // ─────────────────────────────────────────────
        // CONTACT → TCPA/CCPA GATE
        // ─────────────────────────────────────────────
        // If no contact info *after we've offered help*, continue anonymously.
        // Avoid showing this message on the purely educational path.
        Add(FlowConditionHelpers.IfCase(
            "AnonContinueGate",
            ctx =>
                ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need" &&
                string.IsNullOrEmpty(ctx.GetValue<ContactModel>(nameof(ContactModel))?.Email),
            new SimpleActivity(
                "AnonContinue",
                "No contact details were provided; we'll continue anonymously."
            )
        ));

        // If contact info was provided, require TCPA consent (and show CCPA notice)
        // before continuing.
        Add(FlowConditionHelpers.IfCase(
            "TcpacConsentCardGate",
            ctx => !string.IsNullOrEmpty(ctx.GetValue<ContactModel>(nameof(ContactModel))?.Email),
            new AdaptiveCardActivity<TcpaConsentCard, TcpaModel>(
                "TcpaConsent",
                Context,
                c => c.Create()
            )
        ));

        // If consent is given, show the CCPA notice.
        Add(FlowConditionHelpers.IfCase(
            "CcpaNoticeGate",
            ctx => ctx.GetValue<TcpaModel>(nameof(TcpaModel))?.Consent == true,
            new AdaptiveCardActivity<CcpaNoticeCard, CcpaModel>(
                "CcpaNotice",
                Context,
                c => c.Create()
            )
        ));

        // If consent is not given, encourage them to continue anonymously,
        // then ask if they still want to proceed. Only exit if they say no.
        Add(FlowConditionHelpers.IfCase(
            "NoConsentMessageGate",
            ctx => ctx.GetValue<TcpaModel>(nameof(TcpaModel))?.Consent == false,
            new SimpleActivity(
                "NoConsentMessage",
                "We completely respect your choice not to be contacted. We can still walk you through options and answer questions here in chat so you can make an informed decision."
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "NoConsentContinueCheckGate",
            ctx => ctx.GetValue<TcpaModel>(nameof(TcpaModel))?.Consent == false,
            new QuickAnswerActivity(
                "NoConsentContinueCheck",
                "Would you still like to continue and explore life insurance options here, without us reaching out?",
                new[] { "Yes, continue", "No, exit" },
                Context,
                _loggerFactory.CreateLogger<QuickAnswerActivity>()
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "NoConsentExitMessageGate",
            ctx => ctx.GetModelProperty<string>("NoConsentContinueCheck", "answer", string.Empty) == "No, exit",
            new SimpleActivity(
                "NoConsentExitMessage",
                "Thanks. We appreciate your attention. We will send you an email with general insurance information you can review at your convenience."
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "NoConsentExitGate",
            ctx => ctx.GetModelProperty<string>("NoConsentContinueCheck", "answer", string.Empty) == "No, exit",
            new EndActivity("NoConsentExit")
        ));

        // ─────────────────────────────────────────────
        // APPLICABILITY (only for users who clearly want help now)
        // ─────────────────────────────────────────────
        Add(FlowConditionHelpers.IfCase(
            "ApplicabilityCardGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new AdaptiveCardActivity<ApplicabilityCard, ApplicabilityModel>(
                "ApplicabilityCard",
                Context,
                c => c.Create()
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "ApplicabilityCheckGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new QuickAnswerActivity(
                "ApplicabilityCheck",
                "Is this insurance for someone like you (age, family, goals)?",
                new[] { "Yes, continue", "No, not for me" },
                Context,
                _loggerFactory.CreateLogger<QuickAnswerActivity>()
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "ApplicabilityGate",
            ctx =>
                ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need" &&
                ctx.GetModelProperty<string>("ApplicabilityCheck", "answer", string.Empty) == "Yes, continue",
            new AdaptiveCardActivity<InsuranceTypesCard, InsuranceTypesModel>(
                "InsuranceTypes",
                Context,
                c => c.Create()
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "ApplicabilityExitGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new EndActivity(
                "ApplicabilityExit",
                "We appreciate your interest. Your trust is important to us."
            )
        ));

        // ─────────────────────────────────────────────
        // GUIDANCE (only after user has engaged on the main path)
        // ─────────────────────────────────────────────
        Add(FlowConditionHelpers.IfCase(
            "GuidanceCheckGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new QuickAnswerActivity(
                "GuidanceCheck",
                "Would you like AI-assisted guidance on insurance types?",
                new[] { "Yes, start guidance", "No, browse content" },
                Context,
                _loggerFactory.CreateLogger<QuickAnswerActivity>()
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "GuidanceGate",
            ctx =>
                ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need" &&
                ctx.GetModelProperty<string>("GuidanceCheck", "answer", string.Empty) == "Yes, start guidance",
            new AdaptiveCardActivity<LightQualCard, LightQualModel>(
                "LightQualification",
                Context,
                c => c.Create()
            )
        ));

        Add(FlowConditionHelpers.IfCase(
            "BrowseExitGate",
            ctx => ctx.GetModelProperty<string>("ValueCheck", "answer", string.Empty) == "Yes, this is what I need",
            new EndActivity(
                "BrowseExit",
                "Feel free to explore our insurance guides."
            )
        ));

        // ─────────────────────────────────────────────
        // LIGHT QUAL → PERSONALIZED
        // ─────────────────────────────────────────────
        Add(new QuickAnswerActivity(
            "QualCheck",
            "Based on your answers, do we have enough info for personalized guidance?",
            new[] { "Yes, proceed", "No, continue later" },
            Context,
            _loggerFactory.CreateLogger<QuickAnswerActivity>()
        ));

        Add(FlowConditionHelpers.IfCase(
            "QualGate",
            ctx => ctx.GetModelProperty<string>("QualCheck", "answer", string.Empty) == "Yes, proceed",
            new SimpleActivity(
                "PersonalizedFlow",
                "Here's your personalized insurance guidance..."
            )
        ));

        Add(new EndActivity(
            "LaterExit",
            "We'll save your progress. Resume anytime."
        ));
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default) {
        var keywords = new[]
        {
            "new",
            "first time",
            "beginner",
            "newbie",
            "insurance basics"
        };

        return Task.FromResult(
            keywords.Any(k => input?.Contains(k, StringComparison.OrdinalIgnoreCase) == true)
                ? 0.8f
                : 0.0f
        );
    }
}

