namespace ConversaCore.Cards;

public class CardElement {
    public required string Type { get; set; }
    public string? Text { get; set; }
    public string? Id { get; set; }
    public string? Url { get; set; }
    public string? Size { get; set; }
    public string? Weight { get; set; }
    public string? Color { get; set; }
    public bool? Wrap { get; set; }
    public bool? IsSubtle { get; set; }
    public string? HorizontalAlignment { get; set; }
    public bool? IsMultiSelect { get; set; }
    public string? Value { get; set; }
    public string? Style { get; set; }
    public List<CardChoice>? Choices { get; set; }
    public List<CardElement>? Items { get; set; }
    public List<CardElement>? Columns { get; set; }
    public bool Separator { get; set; } = false; // If true, adds a separator before this element
}
