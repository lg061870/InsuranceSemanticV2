namespace ConversaCore.TopicFlow.Rules
{
    public class RuleCandidate
    {
        public string RuleId { get; set; } = string.Empty;
        public string ChunkId { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
