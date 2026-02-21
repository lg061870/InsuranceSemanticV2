# ConversaCore Integrations - Quick Start Guide

## Overview

ConversaCore now supports external integrations via a pass-through (BYOK - Bring Your Own Keys) model. This allows developers to connect their conversations to 6,000+ external services through Zapier webhooks, including:

- **Webscraping.ai** - Competitive intelligence and web scraping
- **CRM Systems** - Salesforce, HubSpot, Zoho
- **Communication** - Twilio SMS, SendGrid emails, Slack
- **Payments** - Stripe, PayPal
- **Analytics** - Google Analytics, Mixpanel, Facebook Analytics
- **Documents** - DocuSign, Adobe Sign, Google Drive

## Architecture

### Pass-Through Model (BYOK)
- ✅ **Zero cost** to ConversaCore platform
- ✅ Customers provide their own API keys/webhook URLs
- ✅ Direct control over integrations
- ✅ Flexible configuration per customer
- ✅ Simple to implement

### Core Components

```
ConversaCore/
  Integrations/
    Core/
      IIntegrationService.cs         # Service interface
      IntegrationService.cs          # HTTP client with retry logic
    Models/
      IntegrationRequest.cs          # Request payload wrapper
      IntegrationResponse.cs         # Response with success/error
      IntegrationConfiguration.cs    # Config per integration
    Zapier/
      ZapierModels.cs               # Request/response DTOs
      ZapierWebhookActivity.cs      # Activity for triggering webhooks
    Exceptions/
      IntegrationException.cs       # Custom exception type
```

## Quick Start - Zapier Integration

### 1. Customer Setup (One-Time)

**Step 1:** Create a Zap at [zapier.com](https://zapier.com)

**Step 2:** Choose "Webhooks by Zapier" as the trigger
- Select "Catch Hook"
- Copy the webhook URL (e.g., `https://hooks.zapier.com/hooks/catch/12345/abcde/`)

**Step 3:** Configure the action
- Example: Send to Google Sheets, Slack, Salesforce, etc.
- Map the fields from the webhook payload

**Step 4:** Test the Zap

### 2. Developer Usage in Topics

```csharp
public class MyInsuranceTopic : TopicFlow
{
    private readonly IIntegrationService _integrationService;
    
    public MyInsuranceTopic(
        TopicWorkflowContext context,
        ILogger<MyInsuranceTopic> logger,
        IConversationContext conversationContext,
        IIntegrationService integrationService) 
        : base(context, logger, "MyInsuranceTopic")
    {
        _integrationService = integrationService;
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        // Step 1: Collect data
        Add(SimpleActivity.Create("collect-quote", ctx =>
        {
            var quoteData = new
            {
                customer_name = "John Doe",
                coverage_amount = 100000,
                annual_premium = 1200,
                quote_date = DateTime.UtcNow
            };
            
            ctx.SetValue("zapier_data", quoteData);
            ctx.SetValue("zapier_webhook_url", 
                "https://hooks.zapier.com/hooks/catch/12345/abcde/");
            
            return Task.FromResult<object?>("Data collected");
        }));

        // Step 2: Trigger Zapier (fire-and-forget)
        Add(SimpleActivity.Create("trigger-zapier", async ctx =>
        {
            var webhookUrl = ctx.GetValue<string>("zapier_webhook_url");
            var data = ctx.GetValue<object>("zapier_data");
            
            var request = new IntegrationRequest<object>
            {
                Payload = data,
                TimeoutSeconds = 10,
                RetryCount = 1
            };
            
            // Store webhook URL in temp context
            ctx.SetValue("__temp_webhook_url", webhookUrl);
            
            var response = await _integrationService.ExecuteAsync<object, object>(
                "Zapier", 
                request,
                CancellationToken.None);
            
            if (response.Success)
            {
                ctx.SetValue("webhook_result", "success");
            }
            
            await Task.CompletedTask;
        }));

        Add(new SimpleActivity("confirmation", 
            "✅ Data sent to Zapier successfully!"));
    }
}
```

## Configuration

### appsettings.json

```json
{
  "Integrations": {
    "Zapier": {
      "Enabled": true,
      "ConnectionType": "CustomerProvided",
      "Settings": {
        "Description": "Zapier webhook integration"
      }
    },
    "DocuSign": {
      "Enabled": false,
      "ConnectionType": "CustomerProvided",
      "BaseUrl": "https://demo.docusign.net/restapi",
      "Settings": {
        "UseSandbox": "true"
      }
    },
    "Stripe": {
      "Enabled": false,
      "ConnectionType": "StripeConnect",
      "BaseUrl": "https://api.stripe.com/v1"
    }
  }
}
```

### DI Registration (Program.cs)

```csharp
// Add integrations support
builder.Services.Configure<IntegrationsConfiguration>(
    configuration.GetSection("Integrations"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();
```

## Webhook Receiver (API Project)

Receive webhooks from external services:

```
POST /api/webhooks/zapier
POST /api/webhooks/docusign
POST /api/webhooks/stripe
POST /api/webhooks/twilio
POST /api/webhooks/{integrationName}
```

Example webhook controller at:
`InsuranceSemanticV2.Api/Endpoints/WebhooksController.cs`

## Demo Topic

Try the Zapier integration demo:

```
InsuranceAgent/Topics/Demo/ZapierIntegrationDemoTopic.cs
```

**Trigger words:** "zapier", "integration", "webhook", "webscraping", "automation"

The demo shows:
1. Explanation of Zapier integration
2. Sample data preparation (insurance quote)
3. Webhook trigger simulation
4. What happens next (external services)

## Use Cases

### 1. Webscraping Competitive Rates
```csharp
var competitorData = new
{
    action = "scrape_competitors",
    insurance_type = "auto",
    zip_code = "90210",
    coverage_amount = 100000
};
// Zap → Webscraping.ai → Google Sheets → Slack notification
```

### 2. CRM Lead Creation
```csharp
var leadData = new
{
    first_name = "Jane",
    last_name = "Smith",
    email = "jane@example.com",
    phone = "555-1234",
    coverage_interest = "Term Life"
};
// Zap → Salesforce Lead → Email Notification
```

### 3. Document Signing
```csharp
var signingRequest = new
{
    customer_email = "john@example.com",
    document_type = "application",
    policy_number = "POL-12345"
};
// Zap → DocuSign → Database Update → SMS Confirmation
```

### 4. Payment Processing
```csharp
var paymentData = new
{
    customer_id = "CUST-123",
    amount = 1200,
    currency = "USD",
    description = "Annual Premium"
};
// Zap → Stripe → Receipt Email → Accounting System
```

## Advanced Features

### Wait for Response Mode

```csharp
// Change ZapierWebhookActivity configuration
WaitForResponse = true;  // Blocks until Zap completes
TimeoutSeconds = 60;     // Max wait time
```

### Error Handling

```csharp
var response = await _integrationService.ExecuteAsync<T, R>(...);

if (!response.Success)
{
    _logger.LogWarning("Integration failed: {Error}", response.ErrorMessage);
    // Handle error - retry, fallback, or notify user
}
```

### Retry Logic

Built-in retry with exponential backoff:
- Default: 3 retries
- Configurable per request
- Automatic backoff: 1s, 2s, 4s

## Security Considerations

### Webhook URL Storage
- Store customer webhook URLs encrypted in database
- Never log full webhook URLs (use masking)
- Validate URLs before calling

### Data Privacy
- GDPR/CCPA: Only send necessary data
- PII: Mask sensitive fields in logs
- Consent: Ensure customer has agreed to data sharing

### Webhook Authentication
- Validate signatures (HMAC) when receiving webhooks
- Use HTTPS only
- Rate limiting on webhook endpoints

## Cost Considerations

### Zapier Pricing
- **Free**: 100 tasks/month
- **Starter**: $19.99/mo (750 tasks)
- **Professional**: $49/mo (2,000 tasks)
- **Team**: $299/mo (50,000 tasks)

Each webhook trigger = 1 task

### Your Costs
- **Development/Testing**: $0-$100/month
- **Production (pass-through)**: $0 (customers pay)
- **Optional**: Partner programs at scale

## Roadmap

See [INTEGRATION_ROADMAP.md](../INTEGRATION_ROADMAP.md) for full implementation plan:

- ✅ **Phase 1 Complete**: Core infrastructure + Zapier
- ⏳ **Phase 2 Next**: DocuSign, Stripe, Twilio, Trulioo
- 📅 **Phase 3**: CRM, Analytics, Additional services
- 📅 **Phase 4**: Extended ecosystem (20+ integrations)

## Support

### Documentation
- [Zapier Webhooks Guide](https://zapier.com/help/create/code-webhooks/send-webhooks-in-zaps)
- [Integration Roadmap](../INTEGRATION_ROADMAP.md)
- [hand-down-regain-control.md](../hand-down-regain-control.md) - Topic flow patterns

### Example Topics
- `ZapierIntegrationDemoTopic.cs` - Basic webhook demo
- `EventTriggerDemoTopic.cs` - Event-driven patterns
- `HandDownDemoTopic.cs` - Sub-topic calling patterns

### Troubleshooting

**Problem**: "Integration 'Zapier' is not enabled"
- **Solution**: Check `appsettings.json` → `Integrations.Zapier.Enabled = true`

**Problem**: Webhook not receiving data
- **Solution**: Check webhook URL, test in Zapier UI, review logs

**Problem**: Timeout errors
- **Solution**: Increase `TimeoutSeconds`, check external service status

## Next Steps

1. ✅ Review this guide
2. ✅ Try the demo topic (say "zapier demo")
3. ✅ Create a test Zap at zapier.com
4. ✅ Build your first integration topic
5. 📚 Read [INTEGRATION_ROADMAP.md](../INTEGRATION_ROADMAP.md) for advanced integrations

---

**Questions?** Check the codebase or integration roadmap for details.
