namespace InsuranceAgent.Configuration
{
    /// <summary>
    /// Configuration settings for OpenAI integration
    /// </summary>
    public class OpenAIConfiguration
    {
        public const string SectionName = "OpenAI";
        
        /// <summary>
        /// OpenAI API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// OpenAI model to use (e.g., gpt-4o-mini, gpt-4, gpt-3.5-turbo)
        /// </summary>
        public string Model { get; set; } = "gpt-4o-mini";
        
        /// <summary>
        /// Maximum tokens for responses
        /// </summary>
        public int MaxTokens { get; set; } = 1000;
        
        /// <summary>
        /// Temperature for response creativity (0.0 to 1.0)
        /// </summary>
        public double Temperature { get; set; } = 0.7;
        
        /// <summary>
        /// Whether OpenAI is properly configured
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && 
                                   ApiKey != "your-openai-api-key-here" && 
                                   ApiKey != "your-dev-openai-api-key-here";
    }
}