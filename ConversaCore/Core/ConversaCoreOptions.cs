namespace ConversaCore.Configuration; 
public class ConversaCoreOptions {
    public string OpenAIApiKey { get; set; } = default!;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}
