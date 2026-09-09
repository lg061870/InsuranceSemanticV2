using ConversaCore.Registration;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Tools;

public sealed class ToolCatalogTests
{
    [Fact]
    public void Catalog_IsImmutableAndCaseInsensitive()
    {
        var descriptor = new ToolDescriptor("profile.lookup", "1", "Profile", "Lookup", typeof(string), typeof(string));
        var catalog = new ToolCatalog(new[] { descriptor });

        Assert.True(catalog.TryGetDescriptor(" PROFILE.LOOKUP ", out var found));
        Assert.Same(descriptor, found);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void AddTool_RegistersSingletonCatalogWithoutActivatingTool()
    {
        var services = new ServiceCollection();
        var descriptor = new ToolDescriptor("profile.lookup", "1", "Profile", "Lookup", typeof(string), typeof(string));
        new ConversaCoreBuilder(services).AddTool(descriptor);
        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IToolCatalog>();
        var second = provider.GetRequiredService<IToolCatalog>();
        Assert.Same(first, second);
        Assert.Same(descriptor, first.Descriptors.Single());
    }

    [Fact]
    public void AssemblyScan_RegistersAttributedToolWithoutConstructingIt()
    {
        var services = new ServiceCollection();
        new ConversaCoreBuilder(services).AddToolsFromAssemblyContaining<ToolCatalogTests>();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IToolCatalog>();
        Assert.True(catalog.TryGetDescriptor("catalog.scan", out var descriptor));
        Assert.Equal(typeof(ScannedTool), descriptor!.ImplementationType);
        Assert.True(catalog.TryGetTypeInfo(typeof(string), out _));
    }

    [ConversaTool("catalog.scan", "1", "Catalog scan", "Test tool")]
    private sealed class ScannedTool : IConversaTool<string, string>
    {
        public ToolDescriptor Descriptor => throw new InvalidOperationException("Must not activate during scan.");
        public ValueTask<ToolResult<string>> ExecuteAsync(string request, ToolExecutionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ToolResult<string>.Success(request));
    }
}
