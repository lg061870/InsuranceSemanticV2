namespace ConversaCore.DTO;

public class UiSearchEvent {
    public string SearchReason { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public UiProgressContext Context { get; set; } = new();
}


