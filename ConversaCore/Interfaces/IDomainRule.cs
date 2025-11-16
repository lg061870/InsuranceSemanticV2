//namespace ConversaCore.Interfaces;
///// <summary>
///// Minimal behavioral contract for any rule type used by the reasoning framework.
///// Domain developers implement this to tell the framework how to serialize and identify a rule.
///// </summary>
//public interface IDomainRule {
//    /// <summary>
//    /// Returns a unique identifier for this rule (used for embeddings and traceability).
//    /// </summary>
//    string GetRuleId();

//    /// <summary>
//    /// Returns a concise textual representation suitable for embeddings or vector indexing.
//    /// Typically includes rule text, conditions, and context.
//    /// </summary>
//    string ToEmbeddingText();

//    /// <summary>
//    /// Optionally provides structured metadata for retrieval or grouping.
//    /// </summary>
//    IDictionary<string, object>? GetMetadata();
//}
