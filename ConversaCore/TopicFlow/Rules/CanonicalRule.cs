using System.Text.Json.Serialization;

namespace ConversaCore.TopicFlow.Rules
{
    public class CanonicalRule
    {
        [JsonPropertyName("rule_id")]
        public string RuleId { get; set; } = string.Empty;

        [JsonPropertyName("chunk_id")]
        public string ChunkId { get; set; } = string.Empty;

        [JsonPropertyName("source_file")]
        public string SourceFile { get; set; } = string.Empty;

        [JsonPropertyName("carrier")]
        public string Carrier { get; set; } = string.Empty;

        [JsonPropertyName("section_path")]
        public string SectionPath { get; set; } = string.Empty;

        [JsonPropertyName("predicate_key")]
        public string PredicateKey { get; set; } = string.Empty;

        [JsonPropertyName("predicate_value")]
        public string PredicateValue { get; set; } = string.Empty;

        [JsonPropertyName("raw_text")]
        public string RawText { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("weight")]
        public double Weight { get; set; } = 1.0;
    }
}
