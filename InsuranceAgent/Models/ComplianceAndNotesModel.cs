namespace InsuranceAgent.Models {
    /// <summary>
    /// Model for compliance and notes data
    /// </summary>
    public class ComplianceAndNotesModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether the user has consented to TCPA
        /// </summary>
        public bool TcpacConsent { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the user has acknowledged CCPA
        /// </summary>
        public bool CcpaAcknowledged { get; set; }
        
        /// <summary>
        /// Gets or sets the agent notes
        /// </summary>
        public string? AgentNotes { get; set; }
    }
}