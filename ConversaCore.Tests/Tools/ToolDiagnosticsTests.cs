using ConversaCore.Registration;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Tools;

public sealed class ToolDiagnosticsTests
{
    [Fact]
    public async Task Executor_EmitsLifecycleDiagnosticsWithoutPayloads()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var diagnostics = new RecordingDiagnostics();
        services.AddSingleton<IToolDiagnostics>(diagnostics);
        new ConversaCoreBuilder(services).AddTool<EchoTool>(new ToolDescriptor("echo", "1", "Echo", "Echo", typeof(string), typeof(string)));
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolExecutor>().ExecuteAsync<string, string>("echo", "secret", new ToolExecutionContext
        { ConversationId = "c", Subject = "s", CorrelationId = "r", Services = provider });
        Assert.Contains(diagnostics.Items, d => d.Kind == ToolDiagnosticKind.Invoking);
        Assert.Contains(diagnostics.Items, d => d.Kind == ToolDiagnosticKind.Completed);
        Assert.Contains(diagnostics.Items, d => d.Kind == ToolDiagnosticKind.Latency);
        Assert.All(diagnostics.Items, d => Assert.DoesNotContain("secret", d.ToString(), StringComparison.Ordinal));
    }

    private sealed class RecordingDiagnostics : IToolDiagnostics
    {
        public List<ToolDiagnostic> Items { get; } = [];
        public void Record(ToolDiagnostic diagnostic) => Items.Add(diagnostic);
    }
    private sealed class EchoTool : IConversaTool<string, string>
    {
        public ToolDescriptor Descriptor => throw new NotSupportedException();
        public ValueTask<ToolResult<string>> ExecuteAsync(string request, ToolExecutionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ToolResult<string>.Success(request));
    }
}
