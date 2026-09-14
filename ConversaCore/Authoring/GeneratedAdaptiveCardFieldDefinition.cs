using System.Collections.ObjectModel;

namespace ConversaCore.Authoring;

/// <summary>Immutable allowlisted input definition for a generated adaptive card.</summary>
public sealed class GeneratedAdaptiveCardFieldDefinition
{
    public GeneratedAdaptiveCardFieldDefinition(
        string id,
        string label,
        GeneratedAdaptiveCardInputKind kind,
        bool isRequired = false,
        string? placeholder = null,
        IEnumerable<GeneratedAdaptiveCardChoice>? choices = null)
    {
        Id = GeneratedAdaptiveCardDefinition.ValidateRequiredText(
            id, nameof(id), GeneratedAdaptiveCardDefinition.MaximumIdLength);
        Label = GeneratedAdaptiveCardDefinition.ValidateRequiredText(
            label, nameof(label), GeneratedAdaptiveCardDefinition.MaximumTextLength);

        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported generated card input kind.");

        Kind = kind;
        IsRequired = isRequired;
        Placeholder = GeneratedAdaptiveCardDefinition.ValidateOptionalText(
            placeholder, nameof(placeholder), GeneratedAdaptiveCardDefinition.MaximumTextLength);

        var copiedChoices = choices?.ToArray() ?? [];
        if (copiedChoices.Any(choice => choice is null))
            throw new ArgumentException("Choices cannot contain null entries.", nameof(choices));
        if (copiedChoices.Length > GeneratedAdaptiveCardDefinition.MaximumChoiceCount)
            throw new ArgumentException(
                $"No more than {GeneratedAdaptiveCardDefinition.MaximumChoiceCount} choices are allowed.",
                nameof(choices));

        if (kind == GeneratedAdaptiveCardInputKind.Choice)
        {
            if (copiedChoices.Length == 0)
                throw new ArgumentException("Choice fields require at least one choice.", nameof(choices));
            if (copiedChoices.Select(choice => choice.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != copiedChoices.Length)
                throw new ArgumentException("Choice values must be unique ignoring case.", nameof(choices));
        }
        else if (copiedChoices.Length != 0)
        {
            throw new ArgumentException("Only choice fields may define choices.", nameof(choices));
        }

        Choices = new ReadOnlyCollection<GeneratedAdaptiveCardChoice>(copiedChoices);
    }

    public string Id { get; }
    public string Label { get; }
    public GeneratedAdaptiveCardInputKind Kind { get; }
    public bool IsRequired { get; }
    public string? Placeholder { get; }
    public IReadOnlyList<GeneratedAdaptiveCardChoice> Choices { get; }
}
