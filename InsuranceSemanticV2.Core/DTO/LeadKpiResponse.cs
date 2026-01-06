namespace InsuranceSemanticV2.Core.DTO;

public class LeadKpiResponse {
    // Overall metrics
    public int TotalLeads { get; set; }
    public int NewLeadsToday { get; set; }
    public int NewLeadsThisWeek { get; set; }
    
    // Temperature breakdown
    public int HotLeads { get; set; }
    public int WarmLeads { get; set; }
    public int ColdLeads { get; set; }
    
    // Status breakdown
    public int ActiveLeads { get; set; }
    public int OnHoldLeads { get; set; }
    public int ToRescueLeads { get; set; }
    public int AbandonedLeads { get; set; }
    
    // Qualification metrics
    public double AverageQualificationScore { get; set; }
    public int QualifiedLeads { get; set; } // Score >= 70
    public int UnqualifiedLeads { get; set; } // Score < 40
    
    // Progress metrics
    public double AverageProgressPercentage { get; set; }
    public int CompletedProfiles { get; set; } // 100% progress
    
    // Time-based metrics
    public double AverageHoursInSystem { get; set; }
    public int LeadsNeedingFollowUp { get; set; }
    
    // Velocity (compared to previous period)
    public double LeadVelocityPercentage { get; set; } // % change from yesterday
    public int LeadsTodayVsYesterday { get; set; }
}
