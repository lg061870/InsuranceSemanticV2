namespace ConversaCore.Cards; 

public static class AdaptiveCardBuilder {
    public static AdaptiveCardModel Create(string title, string subtitle = "") {
        var card = new AdaptiveCardModel();
        card.Body.Add(new CardElement {
            Type = "TextBlock",
            Text = title,
            Size = "Large",
            Weight = "Bolder",
            Wrap = true
        });
        if (!string.IsNullOrEmpty(subtitle)) {
            card.Body.Add(new CardElement {
                Type = "TextBlock",
                Text = subtitle,
                IsSubtle = true,
                Wrap = true
            });
        }
        return card;
    }

    public static CardElement CreateChoiceSet(string id, string defaultValue, params (string Title, string Value)[] choices) {
        return new CardElement {
            Type = "Input.ChoiceSet",
            Id = id,
            Style = "expanded",
            IsMultiSelect = false,
            Value = defaultValue,
            Choices = choices.Select(c => new CardChoice { Title = c.Title, Value = c.Value }).ToList()
        };
    }

    public static CardAction Submit(string title, object data, string style = "positive") {
        return new CardAction {
            Type = "Action.Submit",
            Title = title,
            Style = style,
            Data = data
        };
    }
}