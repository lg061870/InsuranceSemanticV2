namespace ConversaCore.UI.Models;

/// <summary>
/// Defines the visual style and layout of the chat interface.
/// </summary>
public enum ChatStyle
{
    /// <summary>
    /// Default sidebar chat hosted inside the browser window (current implementation).
    /// Appears as a fixed panel on the right side of the page.
    /// </summary>
    SidebarChat = 0,

    /// <summary>
    /// Floating chat window positioned in the lower right corner.
    /// Can be minimized/expanded, floats above page content.
    /// </summary>
    FloatingChat = 1,

    /// <summary>
    /// Full-page chat interface similar to ChatGPT.
    /// Starts with a centered prompt, expands to full chat interface after first message.
    /// </summary>
    ChatWindow = 2
}
