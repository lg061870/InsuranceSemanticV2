using System.Collections.ObjectModel;

namespace ConversaCore.Authoring;

/// <summary>Immutable generated-source configuration for a bounded quick-answer card.</summary>
public sealed class QuickAnswerActivityDefinition
{
    public const int MaximumIdLength = 128;
    public const int MaximumQuestionLength = 1_024;
    public const int MaximumAnswerLength = 256;
    public const int MaximumAnswerCount = 100;

    public QuickAnswerActivityDefinition(
        string activityId,
        string question,
        IEnumerable<string> answers,
        bool isRequired = false)
    {
        ActivityId = ValidateText(activityId, nameof(activityId), MaximumIdLength);
        Question = ValidateText(question, nameof(question), MaximumQuestionLength);
        ArgumentNullException.ThrowIfNull(answers);

        var normalizedAnswers = answers
            .Select((answer, index) => ValidateText(answer, $"{nameof(answers)}[{index}]", MaximumAnswerLength))
            .ToArray();

        if (normalizedAnswers.Length == 0)
            throw new ArgumentException("At least one answer is required.", nameof(answers));
        if (normalizedAnswers.Length > MaximumAnswerCount)
            throw new ArgumentException($"No more than {MaximumAnswerCount} answers are allowed.", nameof(answers));
        if (normalizedAnswers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedAnswers.Length)
            throw new ArgumentException("Answers must be unique ignoring case.", nameof(answers));

        Answers = new ReadOnlyCollection<string>(normalizedAnswers);
        IsRequired = isRequired;
    }

    public string ActivityId { get; }
    public string Question { get; }
    public IReadOnlyList<string> Answers { get; }
    public bool IsRequired { get; }

    private static string ValidateText(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }
}
