namespace InsuranceAgent.Models;

public class LiveAgent {
    public string Name { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public double Rating { get; set; }
    public int Calls { get; set; }
    public int AvgMinutes { get; set; }
    public bool IsAvailable { get; set; }
    public string StatusLabel { get; set; } = "Available Now";
    public string StatusColor { get; set; } = "green";
}
