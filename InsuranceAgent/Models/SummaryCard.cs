namespace InsuranceAgent.Models; 

public record SummaryCard {
    public string Title { get; set; } = "";
    public Dictionary<string, string> Data { get; set; } = new();
}