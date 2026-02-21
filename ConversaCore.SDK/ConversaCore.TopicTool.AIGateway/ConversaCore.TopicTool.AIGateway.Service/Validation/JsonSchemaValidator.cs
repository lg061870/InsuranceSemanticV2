namespace ConversaCore.TopicTool.AIGateway.Service.Validation;

public interface IJsonSchemaValidator
{
    bool IsValid(string json);
}

public sealed class JsonSchemaValidator : IJsonSchemaValidator
{
    public bool IsValid(string json)
    {
        // TODO: Plug in a real JSON schema validator.
        return !string.IsNullOrWhiteSpace(json);
    }
}
