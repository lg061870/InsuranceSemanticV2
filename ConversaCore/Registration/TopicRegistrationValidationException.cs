namespace ConversaCore.Registration;

/// <summary>
/// Thrown by <see cref="TopicRegistrationValidationResult.ThrowIfInvalid"/> when a
/// <see cref="TopicRegistrationValidator"/> pass found one or more problems. The
/// exception message lists every problem in <see cref="Result"/>, not only the first one,
/// matching CC-103's requirement that startup validation report every problem in one
/// actionable summary.
/// </summary>
public sealed class TopicRegistrationValidationException : InvalidOperationException
{
    /// <summary>The aggregated validation result that caused this exception, with every problem found.</summary>
    public TopicRegistrationValidationResult Result { get; }

    /// <summary>
    /// Creates an exception carrying the given invalid result and a message enumerating
    /// every problem in <see cref="TopicRegistrationValidationResult.Errors"/>.
    /// </summary>
    /// <param name="result">The invalid validation result. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public TopicRegistrationValidationException(TopicRegistrationValidationResult result)
        : base(BuildMessage(result ?? throw new ArgumentNullException(nameof(result))))
    {
        Result = result;
    }

    private static string BuildMessage(TopicRegistrationValidationResult result)
    {
        var numberedProblems = result.Errors.Select(
            (error, index) => $"{index + 1}. [{error.Code}] {error.Message}");

        return $"Topic registration validation failed with {result.Errors.Count} problem(s):" +
               Environment.NewLine +
               string.Join(Environment.NewLine, numberedProblems);
    }
}
