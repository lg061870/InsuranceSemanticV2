public class ChatSessionState_obsolete {
        public bool HasConsent { get; set; }
        public string? UserConsentType { get; set; }
        public bool ShowQuestionnaire { get; set; }
        public bool ShowAgentConsole { get; set; }
        public List<string> BeneficiaryInfo { get; set; } = new();
    }
