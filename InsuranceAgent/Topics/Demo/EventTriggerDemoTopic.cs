using ConversaCore.Context;
using ConversaCore.DTO;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InsuranceAgent.Topics.Demo;

/// <summary>
/// Demonstrates the EventTriggerActivity for communicating between
/// topic workflows and the UI layer (progress bar, dialog, notifications).
/// Includes both fire-and-forget and wait-for-response events.
/// </summary>
public class EventTriggerDemoTopic : TopicFlow {
    private readonly ILogger<EventTriggerDemoTopic> _logger;
    private readonly IConversationContext? _conversationContext;

    public static readonly string[] IntentKeywords = {
        "event", "trigger", "demo", "ui", "communication", "custom event"
    };

    public EventTriggerDemoTopic(
        TopicWorkflowContext context,
        ILogger<EventTriggerDemoTopic> logger,
        IConversationContext? conversationContext = null)
        : base(context, logger, name: "EventTriggerDemo") {

        _logger = logger;
        _conversationContext = conversationContext;

        BuildActivityQueue();
    }

    private void BuildActivityQueue() {
        // === 1. Welcome banner (fire-and-forget)
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "ui.notification.show",
            data: new {
                type = "info",
                message = "🚀 Starting EventTrigger demo...",
                duration = 3500
            },
            logger: _logger,
            conversationContext: _conversationContext));

        // === 2. Start progress (fire-and-forget, typed DTO)
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "ui.progress.update",
            data: new UiProgressEvent {
                Stage = "initializing",
                Progress = 10,
                Message = "Initializing demo components",
                Timestamp = DateTime.UtcNow,
                Context = new UiProgressContext {
                    Flow = "event-trigger-demo",
                    Domain = "demo",
                    Consent = "n/a"
                }
            },
            logger: _logger,
            conversationContext: _conversationContext));

        // === 3. Ask user preferences (blocking)
        Add(EventTriggerActivity.CreateWaitForResponse(
            id: "GetUserPreferences",
            eventName: "ui.dialog.preferences",
            responseContextKey: "userPreferences",
            eventData: new {
                title = "Demo Preferences",
                message = "Choose how you’d like to run the EventTrigger demo:",
                options = new[]
                {
                new { id = "quick", label = "Quick Demo", description = "Skip advanced steps" },
                new { id = "full", label = "Full Demo", description = "Show every feature" },
                new { id = "custom", label = "Custom", description = "Let me pick features" }
                }
            },
            responseTimeout: TimeSpan.FromMinutes(2),
            logger: _logger,
            conversationContext: _conversationContext));

        // === 4. Process preferences
        Add(new SimpleActivity("ProcessPreferences", async (ctx, _) =>
        {
            var prefs = ctx.GetValue<object>("userPreferences");
            _logger.LogInformation("[EventTriggerDemo] User preferences: {Preferences}", prefs);
            return ActivityResult.Continue("User preferences processed");
        }));

        // === 5. Conditional UI rendering (advanced vs quick)
        Add(new SimpleActivity("ConditionalDemo", async (ctx, _) =>
        {
            var prefs = ctx.GetValue<dynamic>("userPreferences");
            string? choice = prefs?.id;

            if (choice == "full") {
                var advanced = EventTriggerActivity.CreateFireAndForget(
                    eventName: "ui.components.advanced",
                    data: new {
                        components = new[] { "chart", "datatable", "timeline" },
                        layout = "dashboard",
                        message = "Displaying advanced components"
                    },
                    logger: _logger,
                    conversationContext: _conversationContext);

                return await advanced.RunAsync(ctx, _);
            }

            var simple = EventTriggerActivity.CreateFireAndForget(
                eventName: "ui.notification.show",
                data: new { type = "success", message = "✅ Quick demo mode activated" },
                logger: _logger,
                conversationContext: _conversationContext);

            return await simple.RunAsync(ctx, _);
        }));

        // === 6. Optional feedback form (blocking only for full demo)
        Add(new SimpleActivity("GetFeedback", async (ctx, _) =>
        {
            var prefs = ctx.GetValue<dynamic>("userPreferences");
            if (prefs?.id == "full") {
                var feedback = EventTriggerActivity.CreateWaitForResponse(
                    id: "CollectFeedback",
                    eventName: "ui.form.feedback",
                    responseContextKey: "demoFeedback",
                    eventData: new {
                        title = "Feedback Form",
                        fields = new object[]
                        {
                        new { name = "rating", type = "rating", label = "Rate this demo", max = 5 },
                        new { name = "comments", type = "textarea", label = "Comments", optional = true }
                        }
                    },
                    responseTimeout: TimeSpan.FromMinutes(5),
                    logger: _logger,
                    conversationContext: _conversationContext);

                return await feedback.RunAsync(ctx, _);
            }

            return ActivityResult.Continue("Skipping feedback (quick/custom demo)");
        }));

        // === 7. Mark completion progress (typed UiProgressEvent)
        Add(EventTriggerActivity.CreateFireAndForget(
            eventName: "ui.progress.complete",
            data: new UiProgressEvent {
                Stage = "complete",
                Progress = 100,
                Message = "Demo complete! Displaying summary.",
                Timestamp = DateTime.UtcNow,
                Context = new UiProgressContext {
                    Flow = "event-trigger-demo",
                    Domain = "demo",
                    Consent = "n/a"
                }
            },
            logger: _logger,
            conversationContext: _conversationContext));

        // === 8. Completion notification
        Add(new SimpleActivity("CompleteDemo", async (ctx, _) =>
        {
            var prefs = ctx.GetValue<dynamic>("userPreferences");
            var feedback = ctx.GetValue<object>("demoFeedback");

            string completionMessage = feedback != null
                ? "🎉 Full demo completed with feedback!"
                : prefs?.id == "quick"
                    ? "✅ Quick demo completed!"
                    : "Demo completed successfully!";

            _logger.LogInformation("[EventTriggerDemo] {Message}", completionMessage);

            var final = EventTriggerActivity.CreateFireAndForget(
                eventName: "ui.notification.show",
                data: new {
                    type = "success",
                    message = completionMessage,
                    duration = 4000
                },
                logger: _logger,
                conversationContext: _conversationContext);

            return await final.RunAsync(ctx, _);
        }));

        // === 9. Finish topic
        Add(new CompleteTopicActivity("EventTriggerDemoComplete", "EventTrigger demo completed."));
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        string input = message.ToLowerInvariant();
        int matches = IntentKeywords.Count(input.Contains);
        float confidence = matches > 0 ? Math.Min(1f, matches / 3f) : 0f;
        return Task.FromResult(confidence);
    }
}
