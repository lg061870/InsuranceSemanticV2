using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Zapier;
using ConversaCore.Interfaces;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Simple topic to test Zapier webhook connectivity from the demo app.
/// Shows a quick-answer button to trigger the Zap and then prints
/// whatever response Zapier (or the integration service) returns.
/// </summary>
public class TestZapierTopic : TopicFlow
{
    private readonly ILogger<TestZapierTopic> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IIntegrationService _integrationService;

    // Simple keywords so you can start this topic by typing e.g. "test zapier"
    public static string[] IntentKeywords => new[] { "zapier", "test zapier", "webhook test" };

    public TestZapierTopic(
        TopicWorkflowContext context,
        ILogger<TestZapierTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory,
        IIntegrationService integrationService)
        : base(context, logger, "TestZapierTopic")
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _integrationService = integrationService;
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Intro message
        Add(new SimpleActivity(
            "ZapierTestIntro",
            "This is a simple Zapier test topic. I can send a small payload to your Zapier webhook and show the response we get back."));

        // Quick-answer button to trigger Zap
        Add(new QuickAnswerActivity(
            id: "ZapierTest_Action",
            question: "What would you like to do?",
            answers: new[] { "Trigger test Zap", "Cancel" },
            context: Context,
            logger: _loggerFactory.CreateLogger<QuickAnswerActivity>()
        ));

        // Collect phone number and message via adaptive card so the Zap
        // can echo them back (and eventually forward them to WhatsApp).
        Add(new AdaptiveCardActivity<ZapierTestCard, ZapierTestModel>(
            id: "ZapierTest_CaptureData",
            context: Context,
            cardFactory: card =>
            {
                var currentNumber = Context.GetValue<string>("zapier_phone_number") ?? string.Empty;
                var currentMessage = Context.GetValue<string>("zapier_message_text") ?? string.Empty;
                return new ZapierTestCard().Create(currentNumber, currentMessage);
            },
            modelContextKey: "ZapierTest_Model",
            logger: _loggerFactory.CreateLogger<AdaptiveCardActivity<ZapierTestModel>>()
        ));

        // Prepare payload for Zapier
        Add(new SimpleActivity(
            "ZapierTest_PrepareData",
            (ctx, input) =>
            {
                var lastUser = ctx.GetValue<string>("LastUserMessage") ?? string.Empty;

                var model = ctx.GetValue<ZapierTestModel>("ZapierTest_Model");
                var phoneNumber = model?.PhoneNumber ?? string.Empty;
                var messageText = model?.Message ?? string.Empty;

                ctx.SetValue("zapier_phone_number", phoneNumber);
                ctx.SetValue("zapier_message_text", messageText);

                var payload = new
                {
                    event_type = "test_zapier",
                    phone_number = phoneNumber,
                    message = messageText,
                    last_user_message = lastUser,
                    timestamp_utc = DateTime.UtcNow
                };

                ctx.SetValue("zapier_data", payload);

                // Hard-coded test webhook URL you provided
                ctx.SetValue("zapier_webhook_url", "https://hooks.zapier.com/hooks/catch/25676953/uqqbngv/");
                return Task.FromResult<object?>(null);
            }));

        // Only trigger Zapier when the user chose "Trigger test Zap"
        Add(ConditionalActivity<ZapierWebhookActivity>.Switch(
            "ZapierTest_MaybeTrigger",
            ctx =>
            {
                var model = ctx.GetValue<Dictionary<string, object>>("ZapierTest_Action");
                if (model != null && model.TryGetValue("answer", out var raw))
                {
                    var answer = raw?.ToString() ?? string.Empty;
                    return answer.Equals("Trigger test Zap", StringComparison.OrdinalIgnoreCase)
                        ? "run"
                        : string.Empty;
                }

                return string.Empty;
            },
            new Dictionary<string, Func<string, TopicWorkflowContext, ZapierWebhookActivity>>
            {
                ["run"] = (id, ctx) =>
                {
                    var activity = new ZapierWebhookActivity(
                        id,
                        _integrationService,
                        _loggerFactory.CreateLogger<ZapierWebhookActivity>())
                    {
                        WebhookUrlContextKey = "zapier_webhook_url",
                        DataContextKey = "zapier_data",
                        ResponseContextKey = "zapier_response",
                        EventType = "test_zapier",
                        WaitForResponse = true
                    };

                    return activity;
                }
            },
            defaultBranch: null,
            logger: _loggerFactory.CreateLogger<ConditionalActivity<ZapierWebhookActivity>>()
        ));

        // Show Zapier response (if any)
        Add(new SimpleActivity(
            "ZapierTest_ShowResponse",
            (ctx, input) =>
            {
                var model = ctx.GetValue<Dictionary<string, object>>("ZapierTest_Action");
                if (model != null && model.TryGetValue("answer", out var answerObj))
                {
                    var answer = answerObj?.ToString() ?? string.Empty;
                    if (!answer.Equals("Trigger test Zap", StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult<object?>("Zap test cancelled.");
                    }
                }

                object? resp = null;
                try
                {
                    resp = ctx.GetValue<object>("zapier_response");
                }
                catch
                {
                    // ignore if missing
                }

                // Prefer the raw JSON body if available, otherwise fall back
                // to serializing the typed response we captured.
                string? raw = null;
                try
                {
                    raw = ctx.GetValue<string>("zapier_response_raw");
                }
                catch
                {
                    // ignore if missing
                }

                string json;
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    json = raw;
                }
                else if (resp != null)
                {
                    try
                    {
                        json = JsonSerializer.Serialize(resp, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch
                    {
                        json = resp.ToString() ?? "(non-serializable response)";
                    }
                }
                else
                {
                    json = "(no response payload captured)";
                }

                var message = "✅ Zapier test completed. Here is the raw response we captured (or best effort):\n\n" + json;
                return Task.FromResult<object?>(message);
            }));
    }

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[TestZapierTopic] RunAsync starting (State={State})", State);
        return base.RunAsync(cancellationToken);
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(0.0f);

        if (input.Contains("zapier", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("webhook", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("test zap", StringComparison.OrdinalIgnoreCase))
        {
            // Use a very high confidence so this wins over
            // general-purpose topics like InsuranceBasicsTopic
            // even when their mode flags are active.
            return Task.FromResult(0.99f);
        }

        return Task.FromResult(0.0f);
    }
}
