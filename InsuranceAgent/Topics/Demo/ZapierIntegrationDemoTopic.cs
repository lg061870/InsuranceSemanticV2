using ConversaCore.Context;
using ConversaCore.Integrations.Zapier;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Integrations.Core;

namespace InsuranceAgent.Topics.Demo;

/// <summary>
/// Demo topic showing Zapier webhook integration
/// Demonstrates how to trigger external services (webscraping, analytics, etc.) via Zapier
/// </summary>
public class ZapierIntegrationDemoTopic : TopicFlow
{
    public static string[] IntentKeywords => new[]
    {
        "zapier",
        "integration",
        "webhook",
        "webscraping",
        "external service",
        "automation"
    };

    private readonly ILogger<ZapierIntegrationDemoTopic> _topicLogger;
    private readonly IIntegrationService _integrationService;

    public ZapierIntegrationDemoTopic(
        TopicWorkflowContext context,
        ILogger<ZapierIntegrationDemoTopic> logger,
        IConversationContext conversationContext,
        IIntegrationService integrationService) 
        : base(context, logger, "ZapierIntegrationDemoTopic")
    {
        _topicLogger = logger;
        _integrationService = integrationService;
        
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Activity 1: Welcome message
        Add(new SimpleActivity(
            "welcome-message",
            "Welcome to the Zapier Integration Demo! This demonstrates how ConversaCore can trigger external services via Zapier webhooks."));

        // Activity 2: Explain the integration
        Add(SimpleActivity.Create(
            "explain-integration",
            ctx =>
            {
                var explanation = @"
🔗 **Zapier Integration Overview**

With Zapier webhooks, you can:
- Trigger 6,000+ apps from your conversations
- Access Webscraping.ai for competitive intelligence
- Connect to CRMs (Salesforce, HubSpot)
- Send data to analytics platforms
- Automate document workflows

**Setup Required:**
1. Create a Zap at zapier.com
2. Use 'Webhooks by Zapier' as the trigger
3. Copy the webhook URL
4. Store it in your conversation context

Let's see it in action!";
                
                return Task.FromResult<object?>(explanation);
            }));

        // Activity 3: Collect webhook URL from user
        Add(SimpleActivity.Create(
            "collect-webhook-url",
            ctx =>
            {
                // In a real implementation, this would be an AdaptiveCardActivity
                // For demo purposes, we'll use a simulated webhook URL
                var demoWebhookUrl = "https://hooks.zapier.com/hooks/catch/12345/abcdef/";
                ctx.SetValue("zapier_webhook_url", demoWebhookUrl);
                
                _topicLogger.LogInformation("Demo webhook URL configured: {Url}", demoWebhookUrl);
                return Task.FromResult<object?>("Webhook URL configured (demo mode)");
            }));

        // Activity 4: Prepare data to send
        Add(SimpleActivity.Create(
            "prepare-data",
            ctx =>
            {
                var insuranceQuoteData = new
                {
                    event_type = "insurance_quote_requested",
                    customer_name = "John Doe",
                    coverage_type = "Auto Insurance",
                    coverage_amount = 100000,
                    annual_premium = 1200,
                    quote_date = DateTime.UtcNow,
                    // This could trigger Webscraping.ai to get competitor prices
                    request_competitive_analysis = true,
                    zip_code = "90210"
                };

                ctx.SetValue("zapier_data", insuranceQuoteData);
                
                return Task.FromResult<object?>("Data prepared for Zapier webhook");
            }));

        // Activity 5: Trigger Zapier webhook (fire-and-forget)
        // Note: In real implementation, ZapierWebhookActivity would need ILoggerFactory injected
        Add(SimpleActivity.Create(
            "trigger-zapier-webhook",
            async ctx =>
            {
                var webhookUrl = ctx.GetValue<string>("zapier_webhook_url");
                var data = ctx.GetValue<object>("zapier_data");
                
                _topicLogger.LogInformation("Simulating Zapier webhook trigger to {Url}", webhookUrl);
                
                // In production, this would use ZapierWebhookActivity
                // For demo, we simulate the webhook call
                ctx.SetValue("zapier_response", new 
                { 
                    status = "triggered",
                    timestamp = DateTime.UtcNow,
                    webhook_url = webhookUrl
                });
                
                await Task.Delay(500); // Simulate async call
            }));

        // Activity 6: Confirmation message
        Add(new SimpleActivity(
            "confirmation",
            "✅ Webhook triggered successfully! Your data has been sent to Zapier and will be processed by your Zap."));

        // Activity 7: Show what happens next
        Add(SimpleActivity.Create(
            "next-steps",
            ctx =>
            {
                var nextSteps = @"
📊 **What Happens Next:**

Your Zap can now:
1. **Webscraping.ai**: Scrape competitor insurance rates for comparison
2. **Google Sheets**: Log the quote for analysis
3. **Slack**: Notify your team of the new quote
4. **Salesforce**: Create a lead record
5. **Email**: Send a confirmation to the customer
6. **Analytics**: Track quote metrics in Mixpanel or Google Analytics

All of this happens automatically in the background!

**Advanced Usage:**
- Set `WaitForResponse = true` to get data back from your Zap
- Use webhooks to trigger topics when external events occur
- Chain multiple integrations in a single workflow

Would you like to see the wait-for-response mode?";
                
                return Task.FromResult<object?>(nextSteps);
            }));
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(0.0f);

        foreach (var keyword in IntentKeywords)
        {
            if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Strong confidence when user mentions Zapier/webhooks/integration
                return Task.FromResult(0.9f);
            }
        }

        return Task.FromResult(0.0f);
    }
}
