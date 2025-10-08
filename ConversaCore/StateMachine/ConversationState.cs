namespace ConversaCore.StateMachine
{
    /// <summary>
    /// Represents the possible states of a conversation.
    /// </summary>
    public enum ConversationState
    {
        /// <summary>
        /// The conversation is idle.
        /// </summary>
        Idle,
        
        /// <summary>
        /// The conversation is active.
        /// </summary>
        Active,
        
        /// <summary>
        /// The conversation is waiting for input.
        /// </summary>
        WaitingForInput,
        
        /// <summary>
        /// The conversation is processing input.
        /// </summary>
        Processing,
        
        /// <summary>
        /// The conversation is completed.
        /// </summary>
        Completed,
        
        /// <summary>
        /// The conversation encountered an error.
        /// </summary>
        Error
    }
}