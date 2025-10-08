namespace ConversaCore.Topics {
    /// <summary>
    /// High-level outcome of processing a message in a topic.
    /// </summary>
    public enum TopicResultStatus {
        /// <summary>Message was not handled by this topic.</summary>
        NotHandled,

        /// <summary>Topic is waiting for user input.</summary>
        WaitingForInput,

        /// <summary>Topic completed its flow successfully.</summary>
        Completed,

        /// <summary>An error occurred while processing the message.</summary>
        Error
    }
}
