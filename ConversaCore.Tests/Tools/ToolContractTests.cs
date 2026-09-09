using ConversaCore.Tools;

namespace ConversaCore.Tests.Tools;

public sealed class ToolContractTests
{
    [Fact]
    public void Descriptor_IsImmutableAndRetainsTypedSchemaMetadata()
    {
        var descriptor = new ToolDescriptor(
            " profile.lookup ", " 1.0 ", " Profile lookup ", " Find a profile ",
            typeof(ProfileRequest), typeof(ProfileResult), "request-schema", "result-schema");

        Assert.Equal("profile.lookup", descriptor.ToolId);
        Assert.Equal("1.0", descriptor.Version);
        Assert.Equal(typeof(ProfileRequest), descriptor.RequestType);
        Assert.Equal("request-schema", descriptor.RequestSchema);
    }

    [Theory]
    [InlineData("", "1.0", "name")]
    [InlineData("tool", "", "name")]
    [InlineData("tool", "1.0", "")]
    public void Descriptor_RejectsMissingIdentity(string toolId, string version, string displayName) =>
        Assert.Throws<ArgumentException>(() => new ToolDescriptor(
            toolId, version, displayName, "description", typeof(ProfileRequest), typeof(ProfileResult)));

    [Fact]
    public void ToolResult_SeparatesSuccessAndSafeFailure()
    {
        var success = ToolResult<ProfileResult>.Success(new ProfileResult("Ada"));
        var failure = ToolResult<ProfileResult>.Failure("not_found", "Profile was not found.");

        Assert.True(success.Succeeded);
        Assert.Equal("Ada", success.Value!.Name);
        Assert.False(failure.Succeeded);
        Assert.Null(failure.Value);
        Assert.Equal("not_found", failure.ErrorCode);
    }

    [Fact]
    public async Task ToolContract_ExecutesWithTypedRequestAndNarrowContext()
    {
        var tool = new ProfileTool();
        var context = new ToolExecutionContext
        {
            ConversationId = "conversation-1",
            Subject = "subject-1",
            CorrelationId = "request-1",
            Services = new EmptyServiceProvider()
        };

        var result = await tool.ExecuteAsync(new ProfileRequest("Ada"), context);

        Assert.True(result.Succeeded);
        Assert.Equal("Ada", result.Value!.Name);
    }

    [Fact]
    public async Task ToolContract_ReceivesCancellation()
    {
        var tool = new ProfileTool();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new ToolExecutionContext
        {
            ConversationId = "conversation-1", Subject = "subject-1", CorrelationId = "request-1",
            Services = new EmptyServiceProvider()
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            tool.ExecuteAsync(new ProfileRequest("Ada"), context, cancellation.Token).AsTask());
    }

    private sealed record ProfileRequest(string Name);
    private sealed record ProfileResult(string Name);

    private sealed class ProfileTool : IConversaTool<ProfileRequest, ProfileResult>
    {
        public ToolDescriptor Descriptor { get; } = new(
            "profile.lookup", "1.0", "Profile lookup", "Find a profile",
            typeof(ProfileRequest), typeof(ProfileResult));

        public ValueTask<ToolResult<ProfileResult>> ExecuteAsync(
            ProfileRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ToolResult<ProfileResult>.Success(new ProfileResult(request.Name)));
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
