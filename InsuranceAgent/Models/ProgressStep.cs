namespace InsuranceAgent.Models;

public class ProgressStep {
    public ProgressStep(string label, string key) {
        Label = label;
        EventKey = key;
    }
    public string Label { get; }
    public string EventKey { get; }
    public bool Active { get; set; }
    public bool Completed { get; set; }
}
