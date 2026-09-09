using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ConversaCore.Tools;

/// <summary>Singleton-safe immutable implementation of <see cref="IToolCatalog"/>.</summary>
public sealed class ToolCatalog : IToolCatalog
{
    private readonly IReadOnlyList<ToolDescriptor> _descriptors;
    private readonly FrozenDictionary<string, ToolDescriptor> _byId;
    private readonly FrozenDictionary<Type, JsonTypeInfo> _typeInfos;

    /// <summary>Creates a catalog from descriptor metadata without activating tools.</summary>
    public ToolCatalog(IEnumerable<ToolDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = descriptors.ToArray();
        _byId = _descriptors.ToFrozenDictionary(d => d.ToolId, StringComparer.OrdinalIgnoreCase);
        var resolver = new DefaultJsonTypeInfoResolver();
        _typeInfos = _descriptors
            .SelectMany(d => new[] { d.RequestType, d.ResultType })
            .Distinct()
            .ToFrozenDictionary(type => type, type => resolver.GetTypeInfo(type, JsonSerializerOptions.Default)!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ToolDescriptor> Descriptors => _descriptors;

    /// <inheritdoc />
    public int Count => _descriptors.Count;

    /// <inheritdoc />
    public bool TryGetDescriptor(string toolId, out ToolDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            throw new ArgumentException("Tool ID must not be null, empty, or whitespace.", nameof(toolId));
        return _byId.TryGetValue(toolId.Trim(), out descriptor);
    }

    /// <inheritdoc />
    public bool TryGetTypeInfo(Type type, out JsonTypeInfo? typeInfo)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _typeInfos.TryGetValue(type, out typeInfo);
    }
}
