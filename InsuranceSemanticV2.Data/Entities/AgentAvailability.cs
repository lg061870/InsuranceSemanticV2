namespace InsuranceSemanticV2.Data.Entities;

// --------------------------------------------------------
// 2. AgentAvailability – Agents define general schedule
// --------------------------------------------------------
public class AgentAvailability {
    public int AgentAvailabilityId { get; set; }
    public int AgentId { get; set; }
    public int DayOfWeek { get; set; }          // 0–6
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;

    public Agent? Agent { get; set; }
}
