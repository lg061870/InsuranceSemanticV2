namespace ConversaCore.Tools;
using System.Text.Json.Serialization.Metadata;

/// <summary>Immutable lookup surface for registered tool descriptors.</summary>
public interface IToolCatalog
{
    /// <summary>Gets all registered descriptors in stable registration order.</summary>
    IReadOnlyCollection<ToolDescriptor> Descriptors { get; }

    /// <summary>Gets the number of registered tools.</summary>
    int Count { get; }

    /// <summary>Looks up a tool by case-insensitive stable ID.</summary>
    bool TryGetDescriptor(string toolId, out ToolDescriptor? descriptor);

    /// <summary>Gets cached JSON metadata for a registered request or result type.</summary>
    bool TryGetTypeInfo(Type type, out JsonTypeInfo? typeInfo);
}
