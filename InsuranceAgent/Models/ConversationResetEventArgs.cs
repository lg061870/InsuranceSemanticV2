using System;

namespace InsuranceAgent.Models
{
    /// <summary>
    /// Event args for when a conversation is reset.
    /// </summary>
    public class ConversationResetEventArgs : EventArgs
    {
        public DateTime ResetTime { get; } = DateTime.UtcNow;
        public string? Reason { get; set; }

        public ConversationResetEventArgs(string? reason = null)
        {
            Reason = reason;
        }
    }
}