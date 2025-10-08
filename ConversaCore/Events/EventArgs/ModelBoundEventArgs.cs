// === EventArgs classes ===
namespace ConversaCore.Events;

/// <summary>
/// Event args for when a submission is successfully bound to a model.
/// </summary>
public class ModelBoundEventArgs : EventArgs {
    public object Model { get; }

    public ModelBoundEventArgs(object model) {
        Model = model;
    }
}
