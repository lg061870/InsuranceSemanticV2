using ConversaCore.Runtime;

namespace ConversaCore.UI.Models;

/// <summary>
/// Supplies one immutable host output and the response capability for a correlated interaction.
/// </summary>
/// <remarks>
/// This is the single domain-specific hook exposed by ConversaCore.UI. Notifications are
/// one-way. Interaction requests may be completed exactly once with <see cref="RespondAsync{TResponse}"/>.
/// </remarks>
public sealed class ConversationHostOutputContext
{
    private readonly IConversationRuntime _runtime;

    /// <summary>Creates a host-output callback context for one runtime.</summary>
    public ConversationHostOutputContext(HostOutput output, IConversationRuntime runtime)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>Gets the immutable typed notification or interaction request.</summary>
    public HostOutput Output { get; }

    /// <summary>Gets whether this output accepts a correlated response.</summary>
    public bool RequiresResponse => Output is HostInteractionRequestOutput;

    /// <summary>Sends a typed response for this interaction request through the runtime.</summary>
    /// <typeparam name="TResponse">The response contract declared by the interaction request.</typeparam>
    /// <param name="response">The host's typed response.</param>
    /// <param name="cancellationToken">Cancels response delivery.</param>
    /// <exception cref="InvalidOperationException">Thrown when this output is a one-way notification.</exception>
    public Task RespondAsync<TResponse>(TResponse response, CancellationToken cancellationToken = default)
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(response);
        if (Output is not HostInteractionRequestOutput request)
            throw new InvalidOperationException("A one-way host notification cannot receive a response.");
        return _runtime.RespondToHostInteractionAsync(
            new HostInteractionResponse<TResponse>(request.RequestId, response), cancellationToken);
    }
}
