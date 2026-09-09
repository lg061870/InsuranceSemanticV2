namespace ConversaCore.Runtime;

/// <summary>Required startup configuration for the framework-owned conversation runtime.</summary>
public sealed record ConversationRuntimeOptions
{
    /// <summary>Gets the stable ID of the topic that starts and restarts every conversation.</summary>
    public string StartTopicId { get; }

    /// <summary>Creates runtime options with an explicit registered start topic.</summary>
    /// <param name="startTopicId">Stable ID of the topic to activate on start and reset.</param>
    public ConversationRuntimeOptions(string startTopicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startTopicId);
        StartTopicId = startTopicId;
    }
}
