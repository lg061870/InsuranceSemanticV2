using System;
using System.Collections.Generic;

namespace ConversaCore.TopicTool.Models
{
    /// <summary>
    /// Root in-memory model for the topic designer. Represents a complete design session.
    /// </summary>
    public class TopicDesignerDocument
    {
        public List<TopicModel> Topics { get; set; } = new();

        public List<ValidationMessage> ValidationMessages { get; set; } = new();

        public DesignerSettings Settings { get; set; } = new DesignerSettings();

        public DesignerState State { get; set; } = DesignerState.Idle;
    }

    /// <summary>
    /// Represents a single ConversaCore topic.
    /// </summary>
    public class TopicModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public List<ActivityModel> Activities { get; set; } = new();

        public bool IsExpanded { get; set; }
    }

    /// <summary>
    /// Base class for all activity types in the designer.
    /// </summary>
    public abstract class ActivityModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Logical activity identifier used for branching and navigation.
        /// </summary>
        public string ActivityId { get; set; } = string.Empty;

        public ActivityType Type { get; set; }

        public int Order { get; set; }

        public bool IsExpanded { get; set; }
    }

    public enum ActivityType
    {
        PromptActivity,
        ConditionalActivity,
        AdaptiveCardActivity,
        ApiActivity
    }

    /// <summary>
    /// Prompt/QA style activity where the agent asks a question and captures a response.
    /// </summary>
    public class PromptActivityModel : ActivityModel
    {
        public string Question { get; set; } = string.Empty;

        public AnswerType AnswerType { get; set; } = AnswerType.FreeText;

        public List<AnswerOption> Options { get; set; } = new();

        /// <summary>
        /// Variable name under which the response is stored for later use.
        /// </summary>
        public string StoreResponseVariable { get; set; } = string.Empty;

        public bool IsRequired { get; set; }
    }

    public enum AnswerType
    {
        SingleChoice,
        MultipleChoice,
        FreeText,
        Numeric,
        Boolean
    }

    public class AnswerOption
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Branching activity that routes execution based on a variable's value.
    /// </summary>
    public class ConditionalActivityModel : ActivityModel
    {
        /// <summary>
        /// Name of the variable whose value will be inspected.
        /// </summary>
        public string ConditionVariable { get; set; } = string.Empty;

        public List<ConditionalBranch> Branches { get; set; } = new();

        /// <summary>
        /// Fallback target activity id when no branches match.
        /// </summary>
        public string DefaultTargetActivityId { get; set; } = string.Empty;
    }

    public class ConditionalBranch
    {
        public string MatchValue { get; set; } = string.Empty;

        public string TargetActivityId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Optional: activity that invokes an HTTP API.
    /// </summary>
    public class ApiActivityModel : ActivityModel
    {
        public string Endpoint { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = "GET";

        public Dictionary<string, string> Parameters { get; set; } = new();

        public string StoreResponseVariable { get; set; } = string.Empty;
    }

    /// <summary>
    /// Optional: activity that renders and processes an Adaptive Card.
    /// </summary>
    public class AdaptiveCardActivityModel : ActivityModel
    {
        public string CardTemplateJson { get; set; } = string.Empty;

        public string StoreResponseVariable { get; set; } = string.Empty;
    }

    public class ValidationMessage
    {
        public ValidationSeverity Severity { get; set; }

        public string Message { get; set; } = string.Empty;

        public string TopicName { get; set; } = string.Empty;

        public string ActivityId { get; set; } = string.Empty;
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public class DesignerSettings
    {
        public bool StrictConversaCoreMode { get; set; }

        public bool AllowApiActivities { get; set; }

        public bool AllowAdaptiveCardActivities { get; set; }

        public string SelectedModel { get; set; } = string.Empty;

        public double Temperature { get; set; } = 0.3;
    }

    public enum DesignerState
    {
        Idle,
        Generated,
        Modified,
        HasValidationErrors,
        ReadyToGenerate
    }
}
