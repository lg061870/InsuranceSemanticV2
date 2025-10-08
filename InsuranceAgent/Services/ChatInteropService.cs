using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ConversaCore.Services;

namespace InsuranceAgent.Services;

/// <summary>
/// Provides Blazor ↔ JavaScript interop utilities for the chat window UI,
/// such as scrolling, initialization, and adaptive card handling.
/// </summary>
public class ChatInteropService : IChatInteropService {
    private readonly IJSRuntime _jsRuntime;

    public ChatInteropService(IJSRuntime jsRuntime) {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Initializes the JavaScript chat UI components (typing animation, etc.).
    /// </summary>
    public async Task InitializeChatUIAsync() {
        await _jsRuntime.InvokeVoidAsync("initializeChatUI");
    }

    /// <summary>
    /// Scrolls the chat container to the bottom (to show latest messages).
    /// </summary>
    public async Task ScrollToBottomAsync(ElementReference container) {
        await _jsRuntime.InvokeVoidAsync("scrollChatToBottom", container);
    }

    /// <summary>
    /// Handles form submission from an Adaptive Card and returns processed data.
    /// Currently a pass-through, but can be extended for validation/normalization.
    /// </summary>
    public async Task<Dictionary<string, object>> HandleAdaptiveCardSubmitAsync(
        string actionType,
        Dictionary<string, object> data) {
        // TODO: add per-actionType validation / transforms here if needed
        return await Task.FromResult(data);
    }
}
