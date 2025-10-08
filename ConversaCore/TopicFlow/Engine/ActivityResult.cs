namespace ConversaCore.TopicFlow {
    /// <summary>
    /// Represents the result of running an activity.
    /// </summary>
    public class ActivityResult {
        /// <summary>
        /// Indicates whether the workflow should pause and wait for user input.
        /// </summary>
        public bool IsWaiting { get; private set; }

        /// <summary>
        /// Indicates whether the workflow should pause and wait for a sub-topic to complete.
        /// </summary>
        public bool IsWaitingForSubTopic { get; private set; }

        /// <summary>
        /// The name of the sub-topic to wait for (when IsWaitingForSubTopic is true).
        /// </summary>
        public string? SubTopicName { get; private set; }

        /// <summary>
        /// Optional message returned by the activity (for display in the chat window).
        /// </summary>
        public string? Message { get; private set; }

        /// <summary>
        /// Optional strongly-typed payload or model context returned by the activity.
        /// </summary>
        public object? ModelContext { get; private set; }

        /// <summary>
        /// True if this activity signals the end of the workflow.
        /// </summary>
        public bool IsEnd { get; private set; }

        private ActivityResult() { }

        // === Factory helpers ===

        /// <summary>
        /// Continue workflow with no extra data.
        /// </summary>
        public static ActivityResult Continue() =>
            new ActivityResult();

        /// <summary>
        /// Continue workflow with a simple message string.
        /// </summary>
        public static ActivityResult Continue(string message) =>
            new ActivityResult { Message = message };

        /// <summary>
        /// Continue workflow with an arbitrary object payload.
        /// </summary>
        public static ActivityResult Continue(object payload) =>
            new ActivityResult { ModelContext = payload };

        /// <summary>
        /// End the workflow with a message string.
        /// </summary>
        public static ActivityResult End(string message) =>
            new ActivityResult { IsEnd = true, Message = message };

        /// <summary>
        /// End the workflow with an arbitrary payload.
        /// </summary>
        public static ActivityResult End(object payload) =>
            new ActivityResult { IsEnd = true, ModelContext = payload };

        /// <summary>
        /// Create a result that signals waiting for user input.
        /// </summary>
        public static ActivityResult WaitForInput(string? prompt = null) =>
            new ActivityResult { IsWaiting = true, Message = prompt };


        /// <summary>
        /// Continue workflow with both a message and a payload/model context.
        /// </summary>
        public static ActivityResult Continue(string message, object? modelContext) =>
            new ActivityResult { Message = message, ModelContext = modelContext };

        /// <summary>
        /// Create a result that signals waiting for user input with a structured payload.
        /// </summary>
        public static ActivityResult WaitForInput(object payload, object? modelContext = null) =>
            new ActivityResult { IsWaiting = true, ModelContext = payload, Message = null };

        /// <summary>
        /// Create a result that signals waiting for a sub-topic to complete.
        /// The calling topic will pause until the sub-topic signals completion.
        /// </summary>
        public static ActivityResult WaitForSubTopic(string subTopicName, string? message = null) =>
            new ActivityResult { 
                IsWaitingForSubTopic = true, 
                SubTopicName = subTopicName,
                Message = message ?? $"Waiting for sub-topic '{subTopicName}' to complete"
            };

        /// <summary>
        /// Create a result that signals waiting for a sub-topic with additional context.
        /// </summary>
        public static ActivityResult WaitForSubTopic(string subTopicName, object? modelContext, string? message = null) =>
            new ActivityResult { 
                IsWaitingForSubTopic = true, 
                SubTopicName = subTopicName,
                ModelContext = modelContext,
                Message = message ?? $"Waiting for sub-topic '{subTopicName}' to complete"
            };


    }
}
