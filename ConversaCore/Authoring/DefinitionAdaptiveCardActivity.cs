using ConversaCore.Models;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConversaCore.Authoring;

/// <summary>
/// Renders a bounded generated definition while retaining the standard typed adaptive-card
/// binding, DataAnnotations validation, events, and conversation-state behavior.
/// </summary>
public sealed class DefinitionAdaptiveCardActivity<TModel> : AdaptiveCardActivity<TModel>
    where TModel : class
{
    private readonly GeneratedAdaptiveCardDefinition _definition;

    internal DefinitionAdaptiveCardActivity(
        GeneratedAdaptiveCardDefinition definition,
        TopicWorkflowContext context,
        ILogger<AdaptiveCardActivity<TModel>> logger)
        : base(
            definition?.Id ?? throw new ArgumentNullException(nameof(definition)),
            context,
            logger,
            definition.ModelContextKey ?? typeof(TModel).Name,
            definition.CustomMessage)
    {
        _definition = definition;
        IsRequired = definition.IsRequired;
    }

    protected override string GetCardJson(TopicWorkflowContext context)
    {
        var body = new List<object>();
        if (_definition.Title is not null)
        {
            body.Add(new Dictionary<string, object?>
            {
                ["type"] = "TextBlock",
                ["text"] = _definition.Title,
                ["weight"] = "Bolder",
                ["size"] = "Medium",
                ["wrap"] = true
            });
        }

        body.AddRange(_definition.Fields.Select(RenderField));

        var card = new Dictionary<string, object?>
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = GeneratedAdaptiveCardDefinition.SchemaVersion,
            ["body"] = body,
            ["actions"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "Action.Submit",
                    ["title"] = _definition.SubmitLabel
                }
            }
        };

        return JsonSerializer.Serialize(card);
    }

    protected override TModel? BindModel(Dictionary<string, object> data)
    {
        var json = JsonSerializer.Serialize(data);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new FlexibleIntConverter());
        options.Converters.Add(new FlexibleDecimalConverter());
        options.Converters.Add(new FlexibleDoubleConverter());
        options.Converters.Add(new FlexibleBoolConverter());
        options.Converters.Add(new FlexibleStringListConverter());
        options.Converters.Add(new FlexibleDateTimeConverter());

        _logger.LogDebug(
            "[GeneratedCardBinding] Binding {FieldCount} fields for activity {ActivityId} to {ModelType}",
            data.Count,
            Id,
            typeof(TModel).Name);
        return JsonSerializer.Deserialize<TModel>(json, options);
    }

    protected override Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return base.RunActivity(context, input, cancellationToken);
    }

    public override void Reset()
    {
        if (IsTerminated)
            return;

        base.Reset();
        TransitionTo(ActivityState.Created);
    }

    private static object RenderField(GeneratedAdaptiveCardFieldDefinition field)
    {
        var element = new Dictionary<string, object?>
        {
            ["type"] = field.Kind switch
            {
                GeneratedAdaptiveCardInputKind.Text => "Input.Text",
                GeneratedAdaptiveCardInputKind.Number => "Input.Number",
                GeneratedAdaptiveCardInputKind.Date => "Input.Date",
                GeneratedAdaptiveCardInputKind.Toggle => "Input.Toggle",
                GeneratedAdaptiveCardInputKind.Choice => "Input.ChoiceSet",
                _ => throw new InvalidOperationException($"Unsupported generated card field kind: {field.Kind}.")
            },
            ["id"] = field.Id,
            ["label"] = field.Label,
            ["isRequired"] = field.IsRequired
        };

        if (field.Placeholder is not null && field.Kind is not GeneratedAdaptiveCardInputKind.Toggle)
            element["placeholder"] = field.Placeholder;

        if (field.Kind == GeneratedAdaptiveCardInputKind.Toggle)
        {
            element.Remove("label");
            element["title"] = field.Label;
            element["valueOn"] = "true";
            element["valueOff"] = "false";
        }
        else if (field.Kind == GeneratedAdaptiveCardInputKind.Choice)
        {
            element["style"] = "compact";
            element["choices"] = field.Choices.Select(choice => new Dictionary<string, string>
            {
                ["title"] = choice.Label,
                ["value"] = choice.Value
            }).ToArray();
        }

        return element;
    }
}
