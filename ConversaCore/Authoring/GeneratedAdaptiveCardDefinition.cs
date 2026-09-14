using System.Collections.ObjectModel;

namespace ConversaCore.Authoring;

/// <summary>
/// Immutable, bounded definition rendered by ConversaCore into known Adaptive Card 1.3 shapes.
/// It cannot carry arbitrary Adaptive Card JSON or actions.
/// </summary>
public sealed class GeneratedAdaptiveCardDefinition
{
    public const string SchemaVersion = "1.3";
    public const int MaximumFieldCount = 64;
    public const int MaximumChoiceCount = 100;
    public const int MaximumIdLength = 128;
    public const int MaximumTextLength = 1_024;
    public const int MaximumChoiceValueLength = 256;

    public GeneratedAdaptiveCardDefinition(
        string id,
        IEnumerable<GeneratedAdaptiveCardFieldDefinition> fields,
        string? title = null,
        string submitLabel = "Submit",
        string? modelContextKey = null,
        string? customMessage = null,
        bool isRequired = false)
    {
        Id = ValidateRequiredText(id, nameof(id), MaximumIdLength);
        ArgumentNullException.ThrowIfNull(fields);

        var copiedFields = fields.ToArray();
        if (copiedFields.Any(field => field is null))
            throw new ArgumentException("Fields cannot contain null entries.", nameof(fields));
        if (copiedFields.Length == 0)
            throw new ArgumentException("At least one field is required.", nameof(fields));
        if (copiedFields.Length > MaximumFieldCount)
            throw new ArgumentException($"No more than {MaximumFieldCount} fields are allowed.", nameof(fields));
        if (copiedFields.Select(field => field.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != copiedFields.Length)
            throw new ArgumentException("Field IDs must be unique ignoring case.", nameof(fields));

        Fields = new ReadOnlyCollection<GeneratedAdaptiveCardFieldDefinition>(copiedFields);
        Title = ValidateOptionalText(title, nameof(title), MaximumTextLength);
        SubmitLabel = ValidateRequiredText(submitLabel, nameof(submitLabel), MaximumTextLength);
        ModelContextKey = ValidateOptionalText(modelContextKey, nameof(modelContextKey), MaximumIdLength);
        CustomMessage = ValidateOptionalText(customMessage, nameof(customMessage), MaximumTextLength);
        IsRequired = isRequired;
    }

    public string Id { get; }
    public IReadOnlyList<GeneratedAdaptiveCardFieldDefinition> Fields { get; }
    public string? Title { get; }
    public string SubmitLabel { get; }
    public string? ModelContextKey { get; }
    public string? CustomMessage { get; }
    public bool IsRequired { get; }

    internal static string ValidateRequiredText(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    internal static string? ValidateOptionalText(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (value.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return value;
    }
}
