namespace ConversaCore.Services;

/// <summary>
/// Interface for Direct Line API configuration
/// </summary>
public interface IDirectLineConfig 
{
    /// <summary>
    /// Gets the Direct Line secret
    /// </summary>
    string? Secret { get; }
    
    /// <summary>
    /// Gets the base URL
    /// </summary>
    string? Base { get; }
}