# ChatStyle Feature Documentation

## Overview

The ConversaCore UI library now supports **three different chat interface styles** to accommodate different UX requirements:

1. **SidebarChat** (Default) - Fixed panel embedded within the page
2. **FloatingChat** - Minimizable chat widget floating in lower right corner
3. **ChatWindow** - Full-page interface similar to ChatGPT with initial prompt screen

## Quick Start

### Basic Usage

```csharp
@using ConversaCore.UI.Components
@using ConversaCore.UI.Models

<CustomChatWindowV3 Style="@ChatStyle.SidebarChat" />
```

Register a scoped `IConversationRuntime` in the host. The component resolves it from DI,
subscribes to its typed output stream, and sends start, message, card-submit, and reset
commands directly. The `Runtime` parameter can be supplied explicitly for advanced
composition or tests.

## Chat Styles

### 1. SidebarChat (Default)

**Use Case:** Integrated chat panel within a multi-section page layout.

**Characteristics:**
- Fixed width panel (typically 1/3 of page width)
- Embedded directly in page layout
- Always visible
- Perfect for split-screen layouts where chat is always accessible

**Example:**
```csharp
<div class="flex">
    <div class="w-2/3">
        <!-- Your main content -->
    </div>
    <div class="w-1/3">
        <CustomChatWindowV3 AgentService="@AgentService"
                          Style="@ChatStyle.SidebarChat"
                          SubscribeToEvents="@SubscribeEvents" />
    </div>
</div>
```

**Visual Layout:**
```
┌─────────────────────────┬──────────────┐
│                         │              │
│   Main Content Area     │   Chat       │
│                         │   Panel      │
│                         │              │
└─────────────────────────┴──────────────┘
```

---

### 2. FloatingChat

**Use Case:** Non-intrusive chat that doesn't take permanent screen space.

**Characteristics:**
- Floats in lower right corner with shadow effect
- Can be minimized to just the header or a circular button
- Sits above page content (z-index: 1000)
- Width: 380px, Height: 600px (configurable via CSS)
- Responsive on mobile (full width - 40px)

**Features:**
- ✅ Minimize button in header
- ✅ Smooth expand/collapse animations
- ✅ Toggle button when fully minimized
- ✅ Box shadow for elevation effect

**Example:**
```csharp
<!-- Your full page content -->
<div class="main-content">
    <!-- ... content ... -->
</div>

<!-- Floating chat -->
<CustomChatWindowV3 AgentService="@AgentService"
                  Style="@ChatStyle.FloatingChat"
                  SubscribeToEvents="@SubscribeEvents" />
```

**Visual States:**

*Expanded:*
```
                    ┌──────────────┐
                    │ Sofia [−][×] │
                    │──────────────│
                    │              │
                    │   Messages   │
                    │              │
                    │──────────────│
                    │  Input box   │
                    └──────────────┘
```

*Minimized:*
```
                            ┌─────────┐
                            │ Sofia   │
                            └─────────┘
```

*Fully Collapsed:*
```
                                [💬]
```

---

### 3. ChatWindow

**Use Case:** Full-page conversational interface like ChatGPT or Claude.

**Characteristics:**
- Full viewport height
- Two-phase interface:
  1. **Initial State:** Centered prompt with logo, title, suggestion cards
  2. **Chat State:** Full chat interface (triggered by first message)
- Elegant gradient background
- Suggestion cards for quick starts
- Max width: 900px for readability

**Phases:**

#### Phase 1: Initial Prompt
```csharp
<CustomChatWindowV3 AgentService="@AgentService"
                  Style="@ChatStyle.ChatWindow"
                  SubscribeToEvents="@SubscribeEvents" />
```

**Visual:**
```
╔═══════════════════════════════════════╗
║                                       ║
║              [Avatar]                 ║
║                                       ║
║        Hello, I'm Sofia               ║
║   Your personal insurance advisor     ║
║                                       ║
║   ┌─────────────────────────────┐   ║
║   │ Ask me anything...      [→] │   ║
║   └─────────────────────────────┘   ║
║                                       ║
║   ┌──────────┐  ┌──────────┐       ║
║   │  Life    │  │ Coverage  │       ║
║   │Insurance │  │ Options   │       ║
║   └──────────┘  └──────────┘       ║
║                                       ║
╚═══════════════════════════════════════╝
```

#### Phase 2: Active Chat
After first message is sent, transitions to full chat interface:
```
╔═══════════════════════════════════════╗
║  Sofia • Online                   [×] ║
║───────────────────────────────────────║
║                                       ║
║   User: Tell me about life insurance  ║
║                                       ║
║   Sofia: I'd be happy to help you... ║
║                                       ║
║───────────────────────────────────────║
║  Type your message...            [→] ║
╚═══════════════════════════════════════╝
```

**Suggestion Cards:**
Pre-configured prompts appear as cards in the initial view:
```csharp
<div class="prompt-suggestions">
    <div class="prompt-suggestion-card">
        <div class="suggestion-card-title">Life Insurance</div>
        <div class="suggestion-card-description">Get a personalized quote</div>
    </div>
    <!-- More cards... -->
</div>
```

---

## CSS Architecture

### File Structure

- **chat-enhanced.css** - Base chat styles (messages, cards, animations)
- **chat-styles.css** - Style-specific layouts (NEW)

### Style Classes

Each style applies a specific class to the container:

| Style | CSS Class | Behavior |
|-------|-----------|----------|
| SidebarChat | `.style-sidebar` | Default block layout |
| FloatingChat | `.style-floating` | Fixed positioning + minimization |
| ChatWindow | `.style-chatwindow` | Full viewport + two phases |

### Key CSS Patterns

**Floating Chat Minimization:**
```css
.chat-window-container.style-floating {
    position: fixed;
    bottom: 20px;
    right: 20px;
    width: 380px;
    height: 600px;
    transition: all 0.3s ease;
}

.chat-window-container.style-floating.minimized {
    height: 60px;
    overflow: hidden;
}
```

**ChatWindow Phase Transition:**
```css
.chat-window-container.style-chatwindow .chat-prompt-initial {
    opacity: 1;
    transition: opacity 0.3s ease;
}

.chat-window-container.style-chatwindow.conversation-started .chat-prompt-initial {
    display: none;
}
```

---

## Component Parameters

### CustomChatWindowV3 Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Runtime` | `IConversationRuntime?` | DI resolution | Optional explicit runtime; otherwise the scoped registration is used |
| `Style` | `ChatStyle` | `SidebarChat` | Visual style of the chat interface |
| `AgentService` | `DomainAgentService?` | `null` | Obsolete migration fallback only |
| `SubscribeToEvents` | `Action<CustomChatWindowV3>?` | `null` | Obsolete migration fallback only |

### ChatStyle Enum

```csharp
public enum ChatStyle
{
    SidebarChat = 0,   // Default: Fixed panel in page layout
    FloatingChat = 1,  // Floating widget in lower right
    ChatWindow = 2     // Full-page ChatGPT-style interface
}
```

---

## State Management

### FloatingChat State

```csharp
private bool isMinimized = false;

private void ToggleMinimize()
{
    isMinimized = !isMinimized;
    StateHasChanged();
}
```

### ChatWindow State

```csharp
private bool hasStartedConversation = false;

// Triggered on first message
if (Style == ChatStyle.ChatWindow && !hasStartedConversation)
{
    hasStartedConversation = true;
}
```

---

## Advanced Customization

### Custom Suggestion Cards (ChatWindow)

Modify the initial prompt suggestions:

```csharp
<div class="prompt-suggestions">
    @foreach (var suggestion in InitialSuggestions)
    {
        <div class="prompt-suggestion-card" @onclick="() => UseSuggestion(suggestion.Text)">
            <div class="suggestion-card-title">@suggestion.Title</div>
            <div class="suggestion-card-description">@suggestion.Description</div>
        </div>
    }
</div>
```

### Styling Overrides

Override specific styles in your app's CSS:

```css
/* Make floating chat larger */
.chat-window-container.style-floating {
    width: 450px !important;
    height: 700px !important;
}

/* Change ChatWindow max width */
.chat-window-container.style-chatwindow .chat-interface {
    max-width: 1200px !important;
}
```

---

## Responsive Design

### Mobile Breakpoints

```css
@media (max-width: 768px) {
    /* FloatingChat takes nearly full screen on mobile */
    .chat-window-container.style-floating {
        width: calc(100vw - 40px);
        height: calc(100vh - 100px);
        bottom: 10px;
        right: 10px;
    }

    /* ChatWindow suggestions stack vertically */
    .chat-window-container.style-chatwindow .prompt-suggestions {
        grid-template-columns: 1fr;
    }
}
```

---

## Demo Page

A complete demo is available at `/chat-style-demo`:

```csharp
@page "/chat-style-demo"
<CustomChatWindowV3 AgentService="@AgentService"
                  Style="@selectedStyle"
                  SubscribeToEvents="@SubscribeEvents" />
```

Navigate to `https://localhost:7010/chat-style-demo` to see all three styles in action.

---

## Migration Guide

### Upgrading Existing Code

**Before:**
```csharp
<CustomChatWindowV3 AgentService="@AgentService"
                  SubscribeToEvents="@Subscribe" />
```

**After (with explicit style):**
```csharp
<CustomChatWindowV3 AgentService="@AgentService"
                  Style="@ChatStyle.SidebarChat"
                  SubscribeToEvents="@Subscribe" />
```

**Note:** If you don't specify `Style`, it defaults to `ChatStyle.SidebarChat` (backward compatible).

---

## Best Practices

### When to Use Each Style

| Style | Best For | Avoid When |
|-------|----------|------------|
| **SidebarChat** | Multi-panel layouts, dashboards, always-visible chat | Mobile-first designs, full-screen focus |
| **FloatingChat** | Marketing sites, support chat, non-intrusive assistance | Primary conversation apps, complex forms |
| **ChatWindow** | Dedicated chat applications, onboarding flows, AI assistants | Multi-tasking interfaces, sidebar navigation |

### Performance Considerations

- **FloatingChat**: Renders full DOM even when minimized (consider lazy loading)
- **ChatWindow**: Two complete UIs (initial + chat) - only one rendered at a time
- **SidebarChat**: Always rendered, minimal overhead

---

## Troubleshooting

### Issue: Floating chat appears behind content

**Solution:** Ensure z-index hierarchy:
```css
.chat-window-container.style-floating {
    z-index: 1000 !important;
}
```

### Issue: ChatWindow doesn't transition to chat interface

**Solution:** Check `hasStartedConversation` state is being set:
```csharp
if (Style == ChatStyle.ChatWindow && !hasStartedConversation)
{
    hasStartedConversation = true;
}
```

### Issue: Minimize button not appearing

**Solution:** Ensure Font Awesome is loaded:
```html
<link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" rel="stylesheet" />
```

---

## Future Enhancements

Potential future additions:
- [ ] **PopoutChat**: Separate browser window
- [ ] **MobileDrawer**: Bottom sheet style for mobile
- [ ] **InlineChat**: Contextual chat within forms
- [ ] Custom positioning for FloatingChat (top-left, bottom-left, etc.)
- [ ] Persistent minimize state (localStorage)
- [ ] Custom transition animations
- [ ] Theme variants per style

---

## API Reference

### Methods

```csharp
public class CustomChatWindowV3
{
    // Toggle minimize state (FloatingChat only)
    private void ToggleMinimize();
    
    // Get CSS class for current style
    private string GetStyleClass();
    
    // Render chat content (used by all styles)
    private RenderFragment RenderChatContent();
}
```

### Events

All events are consistent across styles:
- `UserMessageReceived`
- `ConversationStartRequested`
- `ConversationResetRequested`
- `CardSubmitted`

---

## Contributing

To add a new chat style:

1. Add enum value to `ChatStyle.cs`
2. Add CSS class in `chat-styles.css`
3. Update `GetStyleClass()` method
4. Add conditional rendering in component
5. Update documentation

---

## License

Part of ConversaCore framework - same license applies.

---

## Support

For issues or questions:
- Check demo page: `/chat-style-demo`
- Review CSS classes in browser DevTools
- Check console logs for state transitions
- See [hand-down-regain-control.md](../../hand-down-regain-control.md) for event patterns
