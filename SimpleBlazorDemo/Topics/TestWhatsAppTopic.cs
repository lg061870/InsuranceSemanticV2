using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Integrations.Core;
using ConversaCore.Interfaces;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Simple topic to test sending a WhatsApp message via the WhatsApp Business
/// Cloud API using the ConversaCore integration layer.
/// </summary>
public class TestWhatsAppTopic : TopicFlow
{
    private readonly ILogger<TestWhatsAppTopic> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IIntegrationService _integrationService;

    // Simple keywords so you can start this topic by typing e.g. "test whatsapp"
    public static string[] IntentKeywords => new[] { "whatsapp", "test whatsapp", "whatsapp test" };

    public TestWhatsAppTopic(
        TopicWorkflowContext context,
        ILogger<TestWhatsAppTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory,
        IIntegrationService integrationService)
        : base(context, logger, "TestWhatsAppTopic")
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
            "WhatsAppTestIntro",
            "This is a simple WhatsApp test topic. I can send a basic message to a WhatsApp number using your configured Cloud API credentials."));

        // Quick-answer button to trigger WhatsApp send
        Add(new QuickAnswerActivity(
            id: "WhatsAppTest_Action",
            question: "What would you like to do?",
            answers: new[] { "Send WhatsApp test message", "Cancel" },
            context: Context,
            logger: _loggerFactory.CreateLogger<QuickAnswerActivity>()
        ));

        // Collect WhatsApp recipient (and optional custom message) via adaptive card
        Add(new AdaptiveCardActivity<WhatsAppTestCard, WhatsAppTestModel>(
            id: "WhatsAppTest_CaptureRecipient",
            context: Context,
            cardFactory: card =>
            {
                var currentNumber = Context.GetValue<string>("whatsapp_to") ?? string.Empty;
                var currentMessage = Context.GetValue<string>("whatsapp_message_text") ?? string.Empty;
                return new WhatsAppTestCard().Create(currentNumber, currentMessage);
            },
            modelContextKey: "WhatsAppTest_Model",
            logger: _loggerFactory.CreateLogger<AdaptiveCardActivity<WhatsAppTestModel>>()
        ));

        // Prepare payload for WhatsApp
        Add(new SimpleActivity(
            "WhatsAppTest_PrepareData",
            (ctx, input) =>
            {
                var lastUser = ctx.GetValue<string>("LastUserMessage") ?? string.Empty;

                // Pull recipient and optional message from the adaptive card model
                var model = ctx.GetValue<WhatsAppTestModel>("WhatsAppTest_Model");
                var toNumber = model?.PhoneNumber ?? ctx.GetValue("whatsapp_to", string.Empty);

                if (string.IsNullOrWhiteSpace(toNumber))
                {
                    // Surface a message explaining what is missing.
                    var msg = "No WhatsApp recipient configured. Please set context key 'whatsapp_to' to an E.164 phone number (e.g. +15555550123) and try again.";
                    return Task.FromResult<object?>(msg);
                }

                var body = string.IsNullOrWhiteSpace(model?.CustomMessage)
                    ? $"WhatsApp test from TestWhatsAppTopic at {DateTime.UtcNow:O}. Last user message was: '{lastUser}'."
                    : model.CustomMessage!;

                ctx.SetValue("whatsapp_to", toNumber);
                ctx.SetValue("whatsapp_message_text", body);

                return Task.FromResult<object?>(null);
            }));

        // Only trigger WhatsApp when the user chose "Send WhatsApp test message"
        Add(ConditionalActivity<WhatsAppMessageActivity>.Switch(
            "WhatsAppTest_MaybeSend",
            ctx =>
            {
                var model = ctx.GetValue<Dictionary<string, object>>("WhatsAppTest_Action");
                if (model != null && model.TryGetValue("answer", out var answerObj))
                {
                    var answer = answerObj?.ToString() ?? string.Empty;
                    return answer.Equals("Send WhatsApp test message", StringComparison.OrdinalIgnoreCase)
                        ? "run"
                        : string.Empty;
                }

                return string.Empty;
            },
            new Dictionary<string, Func<string, TopicWorkflowContext, WhatsAppMessageActivity>>
            {
                ["run"] = (id, ctx) =>
                {
                    var activity = new WhatsAppMessageActivity(
                        id,
                        _integrationService,
                        _loggerFactory.CreateLogger<WhatsAppMessageActivity>())
                    {
                        PhoneNumberContextKey = "whatsapp_to",
                        MessageTextContextKey = "whatsapp_message_text",
                        ResponseContextKey = "whatsapp_response"
                    };

                    return activity;
                }
            },
            defaultBranch: null,
            logger: _loggerFactory.CreateLogger<ConditionalActivity<WhatsAppMessageActivity>>()
        ));

        // Show WhatsApp response (if any)
        Add(new SimpleActivity(
            "WhatsAppTest_ShowResponse",
            (ctx, input) =>
            {
                var model = ctx.GetValue<Dictionary<string, object>>("WhatsAppTest_Action");
                if (model != null && model.TryGetValue("answer", out var answerObj))
                {
                    var answer = answerObj?.ToString() ?? string.Empty;
                    if (!answer.Equals("Send WhatsApp test message", StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult<object?>("WhatsApp test cancelled.");
                    }
                }

                object? resp = null;
                try
                {
                    resp = ctx.GetValue<object>("whatsapp_response");
                }
                catch
                {
                    // ignore if missing
                }

                string? raw = null;
                try
                {
                    raw = ctx.GetValue<string>("whatsapp_response_raw");
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

                var message = "✅ WhatsApp test completed. Here is the raw response we captured (or best effort):\n\n" + json;
                return Task.FromResult<object?>(message);
            }));
    }

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[TestWhatsAppTopic] RunAsync starting (State={State})", State);
        return base.RunAsync(cancellationToken);
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(0.0f);

        if (input.Contains("whatsapp", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("test whatsapp", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("whatsapp test", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(0.99f);
        }

        return Task.FromResult(0.0f);
    }
}
