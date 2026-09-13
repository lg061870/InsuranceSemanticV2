using ConversaCore.Integrations.Core;
using ConversaCore.Integrations.Models;
using ConversaCore.Integrations.Zapier;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

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

    [Fact]
    public async Task Tool_UsesConfiguredZapierEndpointThroughRealTransport()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"accepted\"}", Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler);
        var transport = CreateTransport(client, "https://hooks.example.test/configured");
        var services = new ServiceCollection().AddSingleton<IIntegrationService>(transport).BuildServiceProvider();

        var result = await new ZapierWebhookTool().ExecuteAsync(
            new ZapierWebhookRequest { EventType = "quote.created", Data = new { LeadId = 42 } },
            new ToolExecutionContext { ConversationId = "c", Subject = "s", CorrelationId = "r", Services = services });

        Assert.True(result.Succeeded);
        Assert.Equal("accepted", result.Value!.Status);
        Assert.Equal(new Uri("https://hooks.example.test/configured"), handler.RequestUri);
        Assert.Contains("quote.created", handler.RequestBody);
    }

    [Fact]
    public async Task Tool_MapsTransportFailureToSafeTypedFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream unavailable")
        });
        using var client = new HttpClient(handler);
        var services = new ServiceCollection()
            .AddSingleton<IIntegrationService>(CreateTransport(client, "https://hooks.example.test/failing"))
            .BuildServiceProvider();

        var result = await new ZapierWebhookTool().ExecuteAsync(
            new ZapierWebhookRequest { EventType = "quote.created", RetryCount = 0 },
            new ToolExecutionContext { ConversationId = "c", Subject = "s", CorrelationId = "r", Services = services });

        Assert.False(result.Succeeded);
        Assert.Equal("integration_failed", result.ErrorCode);
        Assert.DoesNotContain("upstream unavailable", result.ErrorMessage);
    }

    [Fact]
    public async Task Tool_PropagatesCallerCancellation()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler);
        var services = new ServiceCollection()
            .AddSingleton<IIntegrationService>(CreateTransport(client, "https://hooks.example.test/cancel"))
            .BuildServiceProvider();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new ZapierWebhookTool().ExecuteAsync(
                new ZapierWebhookRequest { EventType = "quote.created", RetryCount = 0 },
                new ToolExecutionContext { ConversationId = "c", Subject = "s", CorrelationId = "r", Services = services },
                cancellation.Token));
    }

    private static IntegrationService CreateTransport(HttpClient client, string endpoint) =>
        new(
            new FixedHttpClientFactory(client),
            Options.Create(new IntegrationsConfiguration
            {
                Integrations = new Dictionary<string, IntegrationConfiguration>
                {
                    ["Zapier"] = new() { Enabled = true, BaseUrl = endpoint }
                }
            }),
            NullLogger<IntegrationService>.Instance);

    private sealed class FakeTransport : IIntegrationService
    {
        public Task<IntegrationResponse<TResponse>> ExecuteAsync<TRequest, TResponse>(string integrationName, IntegrationRequest<TRequest> request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntegrationResponse<TResponse> { Success = true, Data = (TResponse)(object)new ZapierWebhookResponse { Status = "ok" } });
        public bool IsEnabled(string integrationName) => true;
        public IEnumerable<string> GetConfiguredIntegrations() => ["Zapier"];
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _response;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
            : this((request, _) => Task.FromResult(response(request))) { }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) =>
            _response = response;

        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _response(request, cancellationToken);
        }
    }
}
