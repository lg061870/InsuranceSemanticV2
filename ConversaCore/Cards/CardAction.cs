namespace ConversaCore.Cards;

public class CardAction {
    public required string Type { get; set; }
    public required string Title { get; set; }
    public string? Style { get; set; }
    public string? IconUrl { get; set; }
    public object? Data { get; set; }
}
