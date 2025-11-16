namespace ConversaCore.DTO;

public class UiProgressEvent {
    public string Stage { get; set; } = "";
    public int Progress { get; set; }
    public string Message { get; set; } = "";
    public string NextStep { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public UiProgressContext Context { get; set; } = new();
    public object Payload { get; set; }
}


