// === EventArgs classes ===
namespace ConversaCore.Events;

/// <summary>
/// Event args for raw adaptive card data submitted by the user.
/// </summary>
public class CardDataReceivedEventArgs : EventArgs {
    public IReadOnlyDictionary<string, object> Data { get; }

    public CardDataReceivedEventArgs(Dictionary<string, object> data) {
        Data = data;
    }
}
