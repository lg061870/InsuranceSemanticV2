using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Models;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Integrations.Zapier;

/// <summary>Typed Zapier capability over the generic integration transport.</summary>
[ConversaTool("zapier.webhook", "1", "Zapier webhook", "Triggers a configured Zapier webhook.")]
public sealed class ZapierWebhookTool : IConversaTool<ZapierWebhookRequest, ZapierWebhookResponse>
{
    /// <inheritdoc />
    public ToolDescriptor Descriptor { get; } = new(
        "zapier.webhook", "1", "Zapier webhook", "Triggers a configured Zapier webhook.",
        typeof(ZapierWebhookRequest), typeof(ZapierWebhookResponse),
        sideEffect: ToolSideEffect.Mutating);

    /// <inheritdoc />
    public async ValueTask<ToolResult<ZapierWebhookResponse>> ExecuteAsync(
        ZapierWebhookRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var transport = context.Services.GetRequiredService<IIntegrationService>();
        var response = await transport.ExecuteAsync<ZapierWebhookRequest, ZapierWebhookResponse>(
            "Zapier", new IntegrationRequest<ZapierWebhookRequest>
            {
                Payload = request,
                Url = request.WebhookUrl,
                TimeoutSeconds = request.TimeoutSeconds,
                RetryCount = request.RetryCount
            }, cancellationToken).ConfigureAwait(false);
        return response.Success && response.Data is not null
            ? ToolResult<ZapierWebhookResponse>.Success(response.Data)
            : ToolResult<ZapierWebhookResponse>.Failure("integration_failed", "The Zapier transport did not complete successfully.");
    }
}
