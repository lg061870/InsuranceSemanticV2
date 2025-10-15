# EventTriggerActivity Usage Guide

The `EventTriggerActivity` allows topics to communicate with the UI layer through custom events. It supports two modes:

## Fire-and-Forget Events

These events are triggered but the topic continues execution immediately without waiting for a response.

```csharp
// Example: Notify UI to show a progress indicator
Add(EventTriggerActivity.CreateFireAndForget(
    "ShowProgress",
    "ui.progress.show",
    new { message = "Processing documents...", percentage = 50 }
));

// Topic continues immediately with next activity
Add(new SimpleActivity("NextStep", (ctx, input) => {
    // This runs right after the event is fired
    return Task.FromResult(ActivityResult.Continue("Continuing...", input));
}));
```

## Blocking Events (Wait for Response)

These events are triggered and the topic suspends execution until the UI provides a response.

```csharp
// Example: Ask UI to show a custom dialog and wait for user choice
Add(EventTriggerActivity.CreateWaitForResponse(
    "ShowCustomDialog",
    "ui.dialog.confirm",
    "user_dialog_choice", // Context key to store response
    new { 
        title = "Important Decision",
        message = "Would you like to proceed with the advanced options?",
        buttons = new[] { "Yes", "No", "Maybe Later" }
    }
));

// This activity only runs AFTER the UI responds
Add(new SimpleActivity("ProcessDialogResponse", (ctx, input) => {
    var userChoice = ctx.GetValue<string>("user_dialog_choice");
    _logger.LogInformation("User chose: {Choice}", userChoice);
    
    if (userChoice == "Yes") {
        // Proceed with advanced flow
        return Task.FromResult(ActivityResult.Continue("Proceeding with advanced options", userChoice));
    } else {
        // Handle other choices
        return Task.FromResult(ActivityResult.Continue("Using standard flow", userChoice));
    }
}));
```

## Integration with Services

To handle these events in your service layer (like `InsuranceAgentService`):

```csharp
public class InsuranceAgentService : IDisposable {
    
    public void RegisterTopic(ITopic topic) {
        // Subscribe to custom events from EventTriggerActivity
        if (topic is ICustomEventTriggeredActivity eventTrigger) {
            eventTrigger.CustomEventTriggered += HandleCustomEvent;
        }
        
        // ... existing topic registration logic
    }
    
    private async void HandleCustomEvent(object? sender, CustomEventTriggeredEventArgs e) {
        try {
            _logger.LogInformation("Received custom event '{EventName}' (WaitForResponse: {WaitForResponse})", 
                e.EventName, e.WaitForResponse);
            
            switch (e.EventName) {
                case "ui.progress.show":
                    await HandleProgressEvent(e);
                    break;
                    
                case "ui.dialog.confirm":
                    await HandleConfirmDialogEvent(e);
                    break;
                    
                default:
                    _logger.LogWarning("Unhandled custom event: {EventName}", e.EventName);
                    break;
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Error handling custom event '{EventName}'", e.EventName);
        }
    }
    
    private async Task HandleProgressEvent(CustomEventTriggeredEventArgs e) {
        // Fire-and-forget event - just update UI
        var data = e.EventData as dynamic;
        await InvokeAsync(() => {
            // Update progress indicator in UI
            // Since this is fire-and-forget, no response needed
        });
    }
    
    private async Task HandleConfirmDialogEvent(CustomEventTriggeredEventArgs e) {
        // Blocking event - show dialog and respond
        var data = e.EventData as dynamic;
        
        await InvokeAsync(async () => {
            // Show custom dialog
            var result = await ShowCustomDialog(data.title, data.message, data.buttons);
            
            // Resume the activity with the response
            if (sender is EventTriggerActivity activity) {
                activity.HandleUIResponse(e.Context, result);
            }
        });
    }
}
```

## Event Data Patterns

### Simple Data
```csharp
EventTriggerActivity.CreateFireAndForget(
    "SimpleEvent",
    "ui.notification",
    "Document processed successfully"
);
```

### Structured Data
```csharp
EventTriggerActivity.CreateWaitForResponse(
    "ComplexEvent",
    "ui.form.custom",
    "form_response",
    new {
        formType = "insurance_application",
        fields = new[] {
            new { name = "policy_type", type = "select", options = new[] { "Auto", "Home", "Life" } },
            new { name = "coverage_amount", type = "number", min = 10000, max = 1000000 }
        }
    }
);
```

## Error Handling

The activity includes comprehensive error handling and logging. It will transition to failed state if:
- Event name is null/empty
- Response context key is missing for blocking events
- Exceptions occur during event triggering

## Context Management

For blocking events, the activity automatically:
- Stores waiting markers in context
- Cleans up markers when response is received
- Stores response data using the provided context key

## Activity State Flow

### Fire-and-Forget:
1. `Running` → Event triggered → `Completed`

### Wait for Response:
1. `Running` → Event triggered → `WaitingForUserInput`
2. UI responds → `HandleUIResponse()` called → `Completed`

This activity follows all ConversaCore patterns for state management, logging, and activity lifecycle.