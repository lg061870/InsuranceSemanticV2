using System;
using System.Collections.Generic;
using System.Linq;
using ConversaCore.TopicTool.Models;
using Newtonsoft.Json;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Request payload for POST /topic-designer/generate.
    /// Matches the v1.0 contract exactly.
    /// </summary>
    internal sealed class TopicDesignerGenerateRequest
    {
        [JsonProperty("intentText")]
        public string IntentText { get; set; } = string.Empty;

        [JsonProperty("settings")]
        public TopicDesignerRequestSettings Settings { get; set; } = new TopicDesignerRequestSettings();

        /// <summary>
        /// Always null for initial generation in v1.0.
        /// Included for forward compatibility.
        /// </summary>
        [JsonProperty("existingStructure")]
        public TopicDesignerDocument? ExistingStructure { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";
    }

    /// <summary>
    /// Request payload for POST /topic-designer/refine.
    /// </summary>
    internal sealed class TopicDesignerRefineRequest
    {
        [JsonProperty("existingStructure")]
        public TopicDesignerDocument ExistingStructure { get; set; } = new TopicDesignerDocument();

        /// <summary>
        /// Optional additional intent or refinement instructions.
        /// </summary>
        [JsonProperty("intentText")]
        public string? IntentText { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";
    }

    internal sealed class TopicDesignerRequestSettings
    {
        [JsonProperty("strictConversaCoreMode")]
        public bool StrictConversaCoreMode { get; set; }

        [JsonProperty("allowApiActivities")]
        public bool AllowApiActivities { get; set; }

        [JsonProperty("allowAdaptiveCardActivities")]
        public bool AllowAdaptiveCardActivities { get; set; }
    }

    /// <summary>
    /// Successful AI gateway response for /topic-designer/* endpoints.
    /// This is intentionally minimal and maps 1:1 into TopicDesignerDocument.
    /// </summary>
    internal sealed class TopicDesignerResponse
    {
        [JsonProperty("topics")]
        public List<TopicDesignerTopicPayload> Topics { get; set; } = new List<TopicDesignerTopicPayload>();
    }

    internal sealed class TopicDesignerTopicPayload
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("activities")]
        public List<TopicDesignerActivityPayload> Activities { get; set; } = new List<TopicDesignerActivityPayload>();
    }

    /// <summary>
    /// Flat activity payload; the Type field determines which ActivityModel subclass it maps to.
    /// Only the fields relevant to the given type are used during mapping.
    /// </summary>
    internal sealed class TopicDesignerActivityPayload
    {
        // Common
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("activityId")]
        public string? ActivityId { get; set; }

        [JsonProperty("order")]
        public int Order { get; set; }

        // PromptActivity
        [JsonProperty("question")]
        public string? Question { get; set; }

        [JsonProperty("answerType")]
        public string? AnswerType { get; set; }

        [JsonProperty("options")]
        public List<AnswerOptionPayload> Options { get; set; } = new List<AnswerOptionPayload>();

        [JsonProperty("storeResponseVariable")]
        public string? StoreResponseVariable { get; set; }

        // ConditionalActivity
        [JsonProperty("conditionVariable")]
        public string? ConditionVariable { get; set; }

        [JsonProperty("branches")]
        public List<ConditionalBranchPayload> Branches { get; set; } = new List<ConditionalBranchPayload>();

        [JsonProperty("defaultTargetActivityId")]
        public string? DefaultTargetActivityId { get; set; }

        // ApiActivity
        [JsonProperty("endpoint")]
        public string? Endpoint { get; set; }

        [JsonProperty("httpMethod")]
        public string? HttpMethod { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        // AdaptiveCardActivity
        [JsonProperty("cardTemplateJson")]
        public string? CardTemplateJson { get; set; }
    }

    internal sealed class AnswerOptionPayload
    {
        [JsonProperty("label")]
        public string? Label { get; set; }

        [JsonProperty("value")]
        public string? Value { get; set; }
    }

    internal sealed class ConditionalBranchPayload
    {
        [JsonProperty("matchValue")]
        public string? MatchValue { get; set; }

        [JsonProperty("targetActivityId")]
        public string? TargetActivityId { get; set; }
    }

    /// <summary>
    /// Error contract from the AI gateway.
    /// </summary>
    internal sealed class TopicDesignerErrorResponse
    {
        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("details")]
        public string? Details { get; set; }

        [JsonProperty("retryable")]
        public bool Retryable { get; set; }
    }

    /// <summary>
    /// Exception used when the AI gateway returns malformed or contract-breaking data.
    /// VSIX code should surface the message in the validation panel rather than trying to repair.
    /// </summary>
    internal sealed class TopicDesignerContractException : Exception
    {
        public TopicDesignerContractException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Deterministic mapper from the wire response payload into the internal TopicDesignerDocument model.
    /// This is the ONLY place where wire JSON structures are translated into the in-memory model.
    /// </summary>
    internal static class TopicDesignerMapper
    {
        public static TopicDesignerDocument MapToDocument(TopicDesignerResponse response, DesignerSettings settings)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var document = new TopicDesignerDocument
            {
                Settings = settings,
                State = DesignerState.Generated
            };

            foreach (var topicPayload in response.Topics ?? Enumerable.Empty<TopicDesignerTopicPayload>())
            {
                if (string.IsNullOrWhiteSpace(topicPayload.Name))
                {
                    throw new TopicDesignerContractException("Topic name is required.");
                }

                var topic = new TopicModel
                {
                    Name = topicPayload.Name.Trim(),
                    Description = (topicPayload.Description ?? string.Empty).Trim()
                };

                // Validate and map activities
                var activities = topicPayload.Activities ?? new List<TopicDesignerActivityPayload>();

                // Ensure order values are unique and sequential starting at 0.
                var orders = activities.Select(a => a.Order).OrderBy(o => o).ToArray();
                for (var i = 0; i < orders.Length; i++)
                {
                    if (orders[i] != i)
                    {
                        throw new TopicDesignerContractException($"Activity order for topic '{topic.Name}' must be sequential starting at 0.");
                    }
                }

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var activityPayload in activities.OrderBy(a => a.Order))
                {
                    if (string.IsNullOrWhiteSpace(activityPayload.ActivityId))
                    {
                        throw new TopicDesignerContractException($"ActivityId is required in topic '{topic.Name}'.");
                    }

                    var activityId = activityPayload.ActivityId.Trim();
                    if (!ids.Add(activityId))
                    {
                        throw new TopicDesignerContractException($"Duplicate ActivityId '{activityId}' in topic '{topic.Name}'.");
                    }

                    if (string.IsNullOrWhiteSpace(activityPayload.Type))
                    {
                        throw new TopicDesignerContractException($"Activity type is required for activity '{activityId}' in topic '{topic.Name}'.");
                    }

                    var type = ParseActivityType(activityPayload.Type);
                    var model = MapActivity(activityPayload, type, topic.Name);
                    model.ActivityId = activityId;
                    model.Type = type;
                    model.Order = activityPayload.Order;

                    topic.Activities.Add(model);
                }

                document.Topics.Add(topic);
            }

            return document;
        }

        private static ActivityType ParseActivityType(string rawType)
        {
            if (!Enum.TryParse<ActivityType>(rawType, ignoreCase: false, out var type))
            {
                throw new TopicDesignerContractException($"Unknown activity type '{rawType}'.");
            }

            return type;
        }

        private static AnswerType ParseAnswerType(string raw, string topicName, string activityId)
        {
            if (!Enum.TryParse<AnswerType>(raw, ignoreCase: false, out var result))
            {
                throw new TopicDesignerContractException($"Invalid AnswerType '{raw}' for activity '{activityId}' in topic '{topicName}'.");
            }

            return result;
        }

        private static ActivityModel MapActivity(TopicDesignerActivityPayload payload, ActivityType type, string topicName)
        {
            switch (type)
            {
                case ActivityType.PromptActivity:
                    if (string.IsNullOrWhiteSpace(payload.Question))
                    {
                        throw new TopicDesignerContractException($"PromptActivity in topic '{topicName}' requires a non-empty question.");
                    }

                    if (string.IsNullOrWhiteSpace(payload.AnswerType))
                    {
                        throw new TopicDesignerContractException($"PromptActivity in topic '{topicName}' must specify AnswerType.");
                    }

                    var answerType = ParseAnswerType(payload.AnswerType, topicName, payload.ActivityId ?? string.Empty);

                    var prompt = new PromptActivityModel
                    {
                        Question = payload.Question.Trim(),
                        AnswerType = answerType,
                        StoreResponseVariable = (payload.StoreResponseVariable ?? string.Empty).Trim(),
                        IsRequired = true,
                    };

                    foreach (var option in payload.Options ?? Enumerable.Empty<AnswerOptionPayload>())
                    {
                        if (string.IsNullOrWhiteSpace(option.Label) || string.IsNullOrWhiteSpace(option.Value))
                        {
                            throw new TopicDesignerContractException($"Answer options must have both label and value in PromptActivity '{payload.ActivityId}'.");
                        }

                        prompt.Options.Add(new AnswerOption
                        {
                            Label = option.Label.Trim(),
                            Value = option.Value.Trim()
                        });
                    }

                    return prompt;

                case ActivityType.ConditionalActivity:
                    if (string.IsNullOrWhiteSpace(payload.ConditionVariable))
                    {
                        throw new TopicDesignerContractException($"ConditionalActivity '{payload.ActivityId}' in topic '{topicName}' requires ConditionVariable.");
                    }

                    var conditional = new ConditionalActivityModel
                    {
                        ConditionVariable = payload.ConditionVariable.Trim(),
                        DefaultTargetActivityId = (payload.DefaultTargetActivityId ?? string.Empty).Trim()
                    };

                    foreach (var branch in payload.Branches ?? Enumerable.Empty<ConditionalBranchPayload>())
                    {
                        if (string.IsNullOrWhiteSpace(branch.MatchValue) || string.IsNullOrWhiteSpace(branch.TargetActivityId))
                        {
                            throw new TopicDesignerContractException($"ConditionalActivity '{payload.ActivityId}' in topic '{topicName}' has a branch with missing MatchValue or TargetActivityId.");
                        }

                        conditional.Branches.Add(new ConditionalBranch
                        {
                            MatchValue = branch.MatchValue.Trim(),
                            TargetActivityId = branch.TargetActivityId.Trim()
                        });
                    }

                    return conditional;

                case ActivityType.ApiActivity:
                    if (string.IsNullOrWhiteSpace(payload.Endpoint))
                    {
                        throw new TopicDesignerContractException($"ApiActivity '{payload.ActivityId}' in topic '{topicName}' requires Endpoint.");
                    }

                    var api = new ApiActivityModel
                    {
                        Endpoint = payload.Endpoint.Trim(),
                        HttpMethod = (payload.HttpMethod ?? "GET").Trim(),
                        StoreResponseVariable = (payload.StoreResponseVariable ?? string.Empty).Trim(),
                        Parameters = payload.Parameters ?? new Dictionary<string, string>()
                    };

                    return api;

                case ActivityType.AdaptiveCardActivity:
                    if (string.IsNullOrWhiteSpace(payload.CardTemplateJson))
                    {
                        throw new TopicDesignerContractException($"AdaptiveCardActivity '{payload.ActivityId}' in topic '{topicName}' requires CardTemplateJson.");
                    }

                    var card = new AdaptiveCardActivityModel
                    {
                        CardTemplateJson = payload.CardTemplateJson,
                        StoreResponseVariable = (payload.StoreResponseVariable ?? string.Empty).Trim()
                    };

                    return card;

                default:
                    throw new TopicDesignerContractException($"Unsupported activity type '{type}'.");
            }
        }
    }
}
