namespace ConversaCore.Authoring;

/// <summary>Immutable label/value pair for an allowlisted generated choice field.</summary>
public sealed class GeneratedAdaptiveCardChoice
{
    public GeneratedAdaptiveCardChoice(string label, string value)
    {
        Label = GeneratedAdaptiveCardDefinition.ValidateRequiredText(
            label, nameof(label), GeneratedAdaptiveCardDefinition.MaximumTextLength);
        Value = GeneratedAdaptiveCardDefinition.ValidateRequiredText(
            value, nameof(value), GeneratedAdaptiveCardDefinition.MaximumChoiceValueLength);
    }

    public string Label { get; }
    public string Value { get; }
}
