# OpenAI Configuration for ConversaCore Insurance Agent

This project now includes OpenAI integration through Semantic Kernel for enhanced conversational capabilities.

## Configuration

### 1. Add your OpenAI API Key

Update your `appsettings.json` or `appsettings.Development.json`:

```json
{
  "OpenAI": {
    "ApiKey": "ApiKey": "__ADD_KEY_HERE__",
    "Model": "gpt-4o-mini",
    "MaxTokens": 1000,
    "Temperature": 0.7
  }
}
```

### 2. Environment Variables (Alternative)

You can also use environment variables:
- `OpenAI__ApiKey` - Your OpenAI API key
- `OpenAI__Model` - Model to use (default: gpt-4o-mini)

### 3. Fallback Mode

If no API key is configured, the system will automatically fall back to keyword-based responses for development and testing.

## Features

- **Smart Intent Recognition**: Uses OpenAI to understand user queries
- **Context-Aware responses**: Maintains conversation context
- **Graceful Fallback**: Falls back to keyword matching if OpenAI is unavailable
- **Insurance Domain**: Specialized prompts for insurance conversations

## Models

Currently configured to use `gpt-4o-mini` for cost-effectiveness. You can change to:
- `gpt-4` - For highest quality responses
- `gpt-3.5-turbo` - For faster, lower-cost responses

## Testing

1. **Without API Key**: System will use keyword-based fallback
2. **With API Key**: Full OpenAI integration with natural language understanding

## Logs

Check the application logs to see which mode is being used:
- `"OpenAI API key not configured. Semantic Kernel will use fallback mode."`
- `"Semantic Kernel configured with OpenAI model: gpt-4o-mini"`