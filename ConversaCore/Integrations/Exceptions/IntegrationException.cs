namespace ConversaCore.Integrations.Exceptions;

/// <summary>
/// Exception thrown when an integration fails
/// </summary>
public class IntegrationException : Exception
{
    public string IntegrationName { get; }
    public int? StatusCode { get; }
    public string? ResponseBody { get; }

    public IntegrationException(string integrationName, string message) 
        : base(message)
    {
        IntegrationName = integrationName;
    }

    public IntegrationException(string integrationName, string message, Exception innerException) 
        : base(message, innerException)
    {
        IntegrationName = integrationName;
    }

    public IntegrationException(string integrationName, string message, int statusCode, string? responseBody = null) 
        : base(message)
    {
        IntegrationName = integrationName;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
