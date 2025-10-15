using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.Demo;

/// <summary>
/// Demo topic showing how to use EventTriggerActivity for UI communication.
/// Demonstrates both fire-and-forget and blocking event patterns.
/// </summary>
public class EventTriggerDemoTopic : TopicFlow {
    
    private readonly ILogger<EventTriggerDemoTopic> _logger;
    private readonly IConversationContext? _conversationContext;
    
    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] { 
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
        // 1. Start with a simple notification (fire-and-forget)
        Add(EventTriggerActivity.CreateFireAndForget(
            "ShowWelcome",
            "ui.notification.show",
            new { 
                type = "info",
                message = "Starting EventTrigger demo...",
                duration = 3000
            },
            _logger,
            _conversationContext
        ));

        // 2. Show progress indicator (fire-and-forget)
        Add(EventTriggerActivity.CreateFireAndForget(
            "ShowProgress",
            "ui.progress.start",
            new { 
                message = "Initializing demo components...",
                percentage = 25
            },
            _logger,
            _conversationContext
        ));

        // 3. Ask for user preferences (blocking - wait for response)
        Add(EventTriggerActivity.CreateWaitForResponse(
            "GetUserPreferences",
            "ui.dialog.preferences",
            "user_preferences", // Response will be stored here
            new {
                title = "Demo Preferences",
                message = "How would you like to proceed with the demo?",
                options = new[] {
                    new { id = "quick", label = "Quick Demo", description = "Skip advanced features" },
                    new { id = "full", label = "Full Demo", description = "Show all capabilities" },
                    new { id = "custom", label = "Custom", description = "Let me choose features" }
                }
            },
            _logger,
            _conversationContext
        ));

        // 4. Process user choice (this only runs after UI responds)
        Add(new SimpleActivity("ProcessPreferences", async (ctx, input) => {
            var preferences = ctx.GetValue<object>("user_preferences");
            _logger.LogInformation("[EventTriggerDemo] User preferences: {Preferences}", preferences);
            
            // Update progress based on choice
            return ActivityResult.Continue("Preferences processed", preferences);
        }));

        // 5. Show conditional content based on user choice
        Add(new SimpleActivity("ConditionalDemo", async (ctx, input) => {
            var prefs = ctx.GetValue<dynamic>("user_preferences");
            if (prefs?.id == "full") {
                // Trigger advanced UI components (fire-and-forget)
                var advancedEvent = EventTriggerActivity.CreateFireAndForget(
                    "ShowAdvanced",
                    "ui.components.advanced",
                    new {
                        components = new[] { "chart", "datatable", "analytics" },
                        layout = "dashboard"
                    },
                    _logger,
                    _conversationContext
                );
                
                // Manually execute the event activity
                return await advancedEvent.RunAsync(ctx, input);
            } else {
                var simpleEvent = EventTriggerActivity.CreateFireAndForget(
                    "ShowSimple",
                    "ui.notification.show",
                    new {
                        type = "success", 
                        message = "Quick demo mode activated"
                    },
                    _logger,
                    _conversationContext
                );
                
                return await simpleEvent.RunAsync(ctx, input);
            }
        }));

        // 6. Ask for feedback if this was a full demo (blocking)
        Add(new SimpleActivity("GetFeedback", async (ctx, input) => {
            var prefs = ctx.GetValue<dynamic>("user_preferences");
            if (prefs?.id == "full") {
                var feedbackEvent = EventTriggerActivity.CreateWaitForResponse(
                    "CollectFeedback",
                    "ui.form.feedback",
                    "demo_feedback",
                    new {
                        title = "Demo Feedback",
                        fields = new object[] {
                            new { name = "rating", type = "rating", label = "How was the demo?", max = 5 },
                            new { name = "comments", type = "textarea", label = "Any comments?", optional = true }
                        }
                    },
                    _logger,
                    _conversationContext
                );
                
                return await feedbackEvent.RunAsync(ctx, input);
            }
            
            return ActivityResult.Continue("No feedback needed for quick demo", input);
        }));

        // 7. Final completion with different messages based on path taken
        Add(new SimpleActivity("CompleteDemo", async (ctx, input) => {
            var feedback = ctx.GetValue<object>("demo_feedback");
            var preferences = ctx.GetValue<dynamic>("user_preferences");
            
            string completionMessage;
            if (feedback != null) {
                completionMessage = "Full demo completed with feedback collected!";
            } else if (preferences?.id == "quick") {
                completionMessage = "Quick demo completed successfully!";
            } else {
                completionMessage = "Custom demo completed!";
            }

            _logger.LogInformation("[EventTriggerDemo] {Message}", completionMessage);
            
            // Final notification (fire-and-forget)
            var finalEvent = EventTriggerActivity.CreateFireAndForget(
                "DemoComplete",
                "ui.notification.show",
                new {
                    type = "success",
                    message = completionMessage,
                    duration = 5000
                },
                _logger,
                _conversationContext
            );
            
            return await finalEvent.RunAsync(ctx, input);
        }));

        // 8. Complete the topic
        Add(new CompleteTopicActivity("EventTriggerDemoComplete", "Demo completed successfully"));
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        // Simple keyword matching for demo purposes
        var input = message.ToLowerInvariant();
        
        int matchCount = 0;
        foreach (var keyword in IntentKeywords) {
            if (input.Contains(keyword)) {
                matchCount++;
            }
        }
        
        var confidence = matchCount > 0 ? Math.Min(1.0f, matchCount / 3.0f) : 0f;
        return Task.FromResult(confidence);
    }
}