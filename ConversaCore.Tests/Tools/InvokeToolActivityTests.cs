using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Tools;

namespace ConversaCore.Tests.Tools;

public sealed class InvokeToolActivityTests
{
    [Fact]
    public async Task Activity_MapsStateAndStoresTypedResult()
    {
        var executor = new RecordingExecutor();
        var activity = new InvokeToolActivity<EchoTool, string, int>(
            "invoke", "length", executor, context => context.GetValue<string>("input")!,
            _ => new ToolExecutionContext { ConversationId = "c", Subject = "s", CorrelationId = "r", Services = new EmptyServices() }, "result");
        var workflow = new TopicWorkflowContext();
        workflow.SetValue("input", "abcd");

        var result = await activity.RunAsync(workflow);

        Assert.Equal(4, workflow.GetValue<ToolResult<int>>("result")!.Value);
        Assert.Equal(4, ((ToolResult<int>)result.ModelContext!).Value);
        Assert.Equal("abcd", executor.Request);
    }

    private sealed class RecordingExecutor : IToolExecutor
    {
        public string? Request { get; private set; }
        public ValueTask<ToolResult<TResult>> ExecuteAsync<TRequest, TResult>(string toolId, TRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            Request = request as string;
            return ValueTask.FromResult((ToolResult<TResult>)(object)ToolResult<int>.Success(Request!.Length));
        }
    }

    private sealed class EchoTool : IConversaTool<string, int>
    {
        public ToolDescriptor Descriptor => throw new NotSupportedException();
        public ValueTask<ToolResult<int>> ExecuteAsync(string request, ToolExecutionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ToolResult<int>.Success(request.Length));
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
