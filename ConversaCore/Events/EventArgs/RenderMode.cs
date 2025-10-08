// === EventArgs classes ===
namespace ConversaCore.Events; 

/// <summary>
/// Render mode hints for how the card should be shown in the chat UI.
/// </summary>
public enum RenderMode {
    Append,   // Add new card
    Replace   // Replace last card
}
