namespace LiveAgentConsole.ViewModels;

public class LeadRowView {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;

    public string Product { get; set; } = string.Empty;
    public string SubProduct { get; set; } = string.Empty;
    public string ProductIcon { get; set; } = "fa-solid fa-shield";
   

    public int Score { get; set; } = 50;
    public string ScoreColor => Score switch {
        >= 80 => "#16a34a", // green
        >= 60 => "#facc15", // yellow
        _ => "#f87171"       // red
    };
    public string ScoreLabel => Score switch {
        >= 80 => "Hot",
        >= 60 => "Warm",
        _ => "Cold"
    };
    public string ScoreLabelColor => Score switch {
        >= 80 => "text-green-700",
        >= 60 => "text-yellow-600",
        _ => "text-red-600"
    };
    public double ScoreOffset => 100 - Score;

    public string Value { get; set; } = "$0";
    public string Status { get; set; } = "New";
    public string NextStep { get; set; } = "Review";

    public string Source { get; set; } = string.Empty;
    public string SourceIcon => Source.ToLowerInvariant() switch {
        var s when s.Contains("web") => "fa-solid fa-globe",
        var s when s.Contains("referral") => "fa-solid fa-user-friends",
        var s when s.Contains("social") => "fa-brands fa-facebook",
        var s when s.Contains("phone") => "fa-solid fa-phone",
        var s when s.Contains("email") => "fa-solid fa-envelope",
        _ => "fa-solid fa-bullhorn"
    };
    public string Campaign { get; set; } = string.Empty;

    // Status helpers
    public bool IsHighPriority => Score >= 80 || Status == "Urgent";
    public bool IsUnassigned => Status == "Unassigned";
    public bool IsOverdue => NextStep.ToLowerInvariant().Contains("follow");

    public int? AssignedAgentId { get; internal set; }

    // Progress tracking properties
    public bool HasContactInfo { get; set; }
    public bool HasHealthInfo { get; set; }
    public bool HasLifeGoals { get; set; }
    public bool HasCoverageIntent { get; set; }
    public bool HasDependents { get; set; }
    public bool HasEmployment { get; set; }
    public bool HasBeneficiaryInfo { get; set; }
    public bool HasAssetsLiabilities { get; set; }

    // Calculate progress percentage (0-100)
    public int ProgressPercentage {
        get {
            int completedSteps = 0;
            int totalSteps = 8;

            if (HasContactInfo) completedSteps++;
            if (HasHealthInfo) completedSteps++;
            if (HasLifeGoals) completedSteps++;
            if (HasCoverageIntent) completedSteps++;
            if (HasDependents) completedSteps++;
            if (HasEmployment) completedSteps++;
            if (HasBeneficiaryInfo) completedSteps++;
            if (HasAssetsLiabilities) completedSteps++;

            return (int)((completedSteps / (double)totalSteps) * 100);
        }
    }

    public string ProgressLabel => $"{ProgressPercentage}% Complete";
}
