using System.ComponentModel.DataAnnotations;
using ConversaCore.Registration;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Tools;

public sealed class ToolExecutorTests
{
    [Fact]
    public async Task Executor_ValidatesAndResolvesToolPerInvocation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var descriptor = new ToolDescriptor("echo", "1", "Echo", "Echoes text", typeof(Request), typeof(string));
        new ConversaCoreBuilder(services).AddTool<EchoTool>(descriptor);
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IToolExecutor>().ExecuteAsync<Request, string>(
            "echo", new Request("hello"), Context(provider));
        Assert.True(result.Succeeded);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public async Task Executor_RejectsInvalidRequestAndAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var descriptor = new ToolDescriptor("secure", "1", "Secure", "Secure", typeof(Request), typeof(string),
            authorization: new ToolAuthorizationPolicy { PolicyName = "write" });
        new ConversaCoreBuilder(services).AddTool<EchoTool>(descriptor);
        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IToolExecutor>();
        var invalid = await executor.ExecuteAsync<Request, string>("secure", new Request(string.Empty), Context(provider));
        var unauthorized = await executor.ExecuteAsync<Request, string>("secure", new Request("x"), Context(provider));
        Assert.Equal("invalid_request", invalid.ErrorCode);
        Assert.Equal("not_authorized", unauthorized.ErrorCode);
    }

    [Fact]
    public async Task Executor_EnforcesConfirmationAndPropagatesCancellation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var descriptor = new ToolDescriptor("mutate", "1", "Mutate", "Mutate", typeof(Request), typeof(string),
            sideEffect: ToolSideEffect.Mutating,
            confirmation: new ToolConfirmationPolicy { Required = true, Purpose = "test" },
            reliability: new ToolReliabilityPolicy { Timeout = TimeSpan.FromSeconds(1) });
        new ConversaCoreBuilder(services).AddTool<EchoTool>(descriptor);
        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IToolExecutor>();
        var denied = await executor.ExecuteAsync<Request, string>("mutate", new Request("x"), Context(provider));
        Assert.Equal("confirmation_required", denied.ErrorCode);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync<Request, string>(
            "mutate", new Request("x"), Context(provider) with { ConfirmationGranted = true }, cancellation.Token).AsTask());
    }

    private static ToolExecutionContext Context(IServiceProvider services) => new()
    {
        ConversationId = "conversation", Subject = "subject", CorrelationId = Guid.NewGuid().ToString(), Services = services
    };

    [Fact]
    public async Task Executor_RejectsToolOutsideTopicAllowlist()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var descriptor = new ToolDescriptor("echo", "1", "Echo", "Echoes text", typeof(Request), typeof(string));
        new ConversaCoreBuilder(services).AddTool<EchoTool>(descriptor);
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IToolExecutor>().ExecuteAsync<Request, string>(
            "echo", new Request("hello"), Context(provider) with
            { AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "other" } });
        Assert.Equal("tool_not_allowed", result.ErrorCode);
    }

    private sealed record Request([property: Required] string Value);

    private sealed class EchoTool : IConversaTool<Request, string>
    {
        public ToolDescriptor Descriptor => throw new NotSupportedException();
        public ValueTask<ToolResult<string>> ExecuteAsync(Request request, ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ToolResult<string>.Success(request.Value));
        }
    }
}
