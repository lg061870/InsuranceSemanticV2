namespace ConversaCore.Cards;

public class AdaptiveCardModel {
    public string Type { get; set; } = "AdaptiveCard";
    public string Version { get; set; } = "1.5";
    public string Schema { get; set; } = "http://adaptivecards.io/schemas/adaptive-card.json";
    public string? Style { get; set; }
    public List<CardElement> Body { get; set; } = new();
    public List<CardAction> Actions { get; set; } = new();
}
