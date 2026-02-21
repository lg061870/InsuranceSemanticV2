using System.Collections.Generic;

namespace ConversaCore.TopicTool.Models
{
    /// <summary>
    /// Scope of a document collection.
    /// Global collections are shared across topics; Topic scope binds to a specific topic.
    /// </summary>
    public enum DocumentCollectionScope
    {
        Global,
        Topic
    }

    /// <summary>
    /// Configuration for a single document collection.
    /// </summary>
    public sealed class DocumentCollectionConfig
    {
        /// <summary>
        /// Logical name of the collection (e.g., "LifeInsuranceBasics").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Scope for this collection (Global or Topic-specific).
        /// </summary>
        public DocumentCollectionScope Scope { get; set; } = DocumentCollectionScope.Global;

        /// <summary>
        /// Optional topic name this collection is associated with when Scope == Topic.
        /// </summary>
        public string TopicName { get; set; } = string.Empty;

        /// <summary>
        /// One or more source folders under the host project's Documents/ tree.
        /// Paths are relative to the host project root.
        /// </summary>
        public List<string> SourceFolders { get; set; } = new List<string>();

        /// <summary>
        /// File patterns to include (e.g., "**/*.md", "**/*.pdf").
        /// </summary>
        public List<string> IncludePatterns { get; set; } = new List<string>();

        /// <summary>
        /// Optional file patterns to exclude.
        /// </summary>
        public List<string> ExcludePatterns { get; set; } = new List<string>();

        /// <summary>
        /// Optional free-form options bag (chunking, max tokens, language hints, etc.).
        /// </summary>
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Root config for all ConversaCore document collections in a host project.
    /// Serialized to .conversacore.documents.json at the project root.
    /// </summary>
    public sealed class DocumentCollectionsConfigRoot
    {
        /// <summary>
        /// Optional application- or solution-level prefix used for vector collection naming.
        /// </summary>
        public string AppPrefix { get; set; } = string.Empty;

        /// <summary>
        /// All configured document collections.
        /// </summary>
        public List<DocumentCollectionConfig> Collections { get; set; } = new List<DocumentCollectionConfig>();
    }
}
