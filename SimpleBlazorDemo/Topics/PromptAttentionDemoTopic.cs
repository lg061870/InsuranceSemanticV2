using System;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Minimal demo topic that exercises ChatPromptAttentionActivity so
/// you can see the chat prompt visually ask for input.
/// Triggered by phrases like "prompt attention demo".
/// </summary>
public class PromptAttentionDemoTopic : TopicFlow
{
    private readonly ILogger<PromptAttentionDemoTopic> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public PromptAttentionDemoTopic(
        TopicWorkflowContext context,
        ILogger<PromptAttentionDemoTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory)
        : base(context, logger, "PromptAttentionDemoTopic")
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Brief explanation message
        Add(new SimpleActivity(
            "PromptDemoIntro",
            "This is the prompt-attention demo topic. I will first collect a short reply, then highlight the bottom prompt to show you the attention effect."));

        // Collect a short free-form reply via the lightweight card
        Add(new WaitForUserInputActivity(
            id: "PromptDemo_WaitForUser",
            context: Context,
            logger: _loggerFactory.CreateLogger<AdaptiveCardActivity<WaitForUserInputModel>>(),
            prompt: "What would you like to say?"));

        Add(new SimpleActivity(
            "PromptDemo_Echo",
            (ctx, input) =>
            {
                var user = ctx.GetValue<string>("LastUserMessage") ?? string.Empty;
                var reply = string.IsNullOrWhiteSpace(user)
                    ? "I didn't catch anything, but the prompt-attention effect should have been visible."
                    : $"You said: '{user}'. This concludes the prompt-attention demo.";

                return Task.FromResult<object?>(reply);
            }));

        // After the demo response, offer a couple of suggestions and
        // visually ping the bottom prompt so you can continue.
        Add(new ShowSuggestionsActivity(
            id: "PromptDemoSuggestions",
            suggestions: new[]
            {
                "prompt attention demo",
                "insurance basics"
            }));

        // Compute a context-aware attention message based on the last
        // user input, so the prompt placeholder can be specific to
        // what they just said (e.g., "I can't tie my shoes").
        Add(new SimpleActivity(
            "PromptDemo_AttentionPrep",
            async (ctx, input) =>
            {
                var last = ctx.GetValue<string>("LastUserMessage") ?? string.Empty;

                string attentionText;
                if (string.IsNullOrWhiteSpace(last))
                {
                    attentionText = "Please elaborate your comment.";
                }
                else
                {
                    attentionText = $"Please elaborate on: \"{last}\" — how is this related to insurance?";
                }

                ctx.SetValue("PromptAttentionMessage", attentionText);
                return await Task.FromResult<object?>(null);
            }));

        Add(new ChatPromptAttentionActivity(
            id: "PromptDemo_Attention",
            message: null,
            durationMs: 4000,
            logger: _loggerFactory.CreateLogger<ChatPromptAttentionActivity>()));
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(0.0f);

        if (input.Contains("prompt attention demo", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("prompt demo", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(0.99f);
        }

        return Task.FromResult(0.0f);
    }
}
