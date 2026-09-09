using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Models;
using ConversaCore.Integrations.Zapier;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Tools;

public sealed class ZapierWebhookToolTests
{
    [Fact]
    public async Task Tool_UsesIntegrationServiceAndReturnsTypedResponse()
    {
        var services = new ServiceCollection().AddSingleton<IIntegrationService, FakeTransport>().BuildServiceProvider();
        var result = await new ZapierWebhookTool().ExecuteAsync(new ZapierWebhookRequest { WebhookUrl = "https://hooks.example.test" },
            new ToolExecutionContext { ConversationId = "c", Subject = "s", CorrelationId = "r", Services = services });
        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.Value!.Status);
    }

    private sealed class FakeTransport : IIntegrationService
    {
        public Task<IntegrationResponse<TResponse>> ExecuteAsync<TRequest, TResponse>(string integrationName, IntegrationRequest<TRequest> request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntegrationResponse<TResponse> { Success = true, Data = (TResponse)(object)new ZapierWebhookResponse { Status = "ok" } });
        public bool IsEnabled(string integrationName) => true;
        public IEnumerable<string> GetConfiguredIntegrations() => ["Zapier"];
    }
}
