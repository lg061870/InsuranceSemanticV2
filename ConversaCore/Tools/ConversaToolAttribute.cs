namespace ConversaCore.Tools;

/// <summary>Declares descriptor metadata for assembly-based tool registration.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ConversaToolAttribute : Attribute
{
    /// <summary>Creates assembly-scan metadata for a tool implementation.</summary>
    public ConversaToolAttribute(string toolId, string version, string displayName, string description = "")
    {
        ToolId = toolId;
        Version = version;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>Gets the stable tool ID.</summary>
    public string ToolId { get; }
    /// <summary>Gets the tool contract version.</summary>
    public string Version { get; }
    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; }
    /// <summary>Gets the description.</summary>
    public string Description { get; }
}
