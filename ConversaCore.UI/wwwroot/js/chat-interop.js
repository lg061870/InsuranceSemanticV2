// Enhanced chat-interop.js
let homeComponentReference = null;
let chatDotNetRef = null;

// Register the Home component for JS interop
window.registerHomeComponent = function (dotNetRef) {
    homeComponentReference = dotNetRef;
    console.log("Home component registered for JS interop");
};

// Clean up the Home component reference
window.cleanupHomeComponent = function () {
    if (homeComponentReference) {
        homeComponentReference.dispose();
        homeComponentReference = null;
        console.log("Home component reference cleaned up");
    }
};

// Register chat component for direct JS interop
window.registerChatComponent = function (dotNetRef) {
    chatDotNetRef = dotNetRef;
    console.log("Chat component registered for JS interop");
};

// Clean up the chat component reference
window.cleanupChatComponent = function () {
    if (chatDotNetRef) {
        chatDotNetRef.dispose();
        chatDotNetRef = null;
        console.log("Chat component reference cleaned up");
    }
};

// Send an event to the bot
window.sendEventToBot = function (eventName, eventData) {
    if (window.webchatStore) {
        window.webchatStore.dispatch({
            type: 'WEB_CHAT/SEND_EVENT',
            payload: {
                name: eventName,
                value: eventData
            }
        });
        console.log(`Event sent to bot: ${eventName}`, eventData);
    } else {
        console.error("WebChat store not initialized, cannot send event to bot");
    }
};

// Enhanced scroll to bottom with smooth animation
window.scrollChatToBottom = function (element) {
    if (element) {
        element.scrollTo({
            top: element.scrollHeight,
            behavior: 'smooth'
        });
    }
};

// Legacy method kept for compatibility
window.scrollToBottom = function (element) {
    window.scrollChatToBottom(element);
};

// Enhanced chat UI initialization with animations and event handlers
window.initializeChatUI = function () {
    console.log('Enhanced Chat UI initialized');
    
    // Set up auto-expanding textarea for message input
    const messageInput = document.querySelector('.message-input');
    if (messageInput) {
        messageInput.addEventListener('input', function() {
            // Reset height to auto to get correct scrollHeight
            this.style.height = 'auto';
            // Set new height based on scrollHeight, with max height limit
            const newHeight = Math.min(this.scrollHeight, 120);
            this.style.height = newHeight + 'px';
        });
    }
    
    // Add smooth animation to message container
    const messagesContainer = document.querySelector('.messages-container');
    if (messagesContainer) {
        messagesContainer.style.scrollBehavior = 'smooth';
    }
    
    // Add copy to clipboard functionality
    document.querySelectorAll('.message-action-button[title="Copy"]').forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();
        });
    });
    
    // Add hover effects for message actions
    document.querySelectorAll('.message').forEach(message => {
        message.addEventListener('mouseenter', function() {
            const actions = this.querySelector('.message-actions');
            if (actions) actions.style.opacity = '1';
        });
        
        message.addEventListener('mouseleave', function() {
            const actions = this.querySelector('.message-actions');
            if (actions) actions.style.opacity = '0';
        });
    });
};

// Enhanced event tracking with toast notifications
window.recordChatEvent = function (eventType, eventData) {
    console.log(`Chat event: ${eventType}`, eventData);
    
    // Analytics tracking
    if (window.dataLayer) {
        window.dataLayer.push({
            'event': 'chat_event',
            'chat_event_type': eventType,
            'chat_event_data': eventData
        });
    }
    
    // Show toast notification for certain events
    if (eventType === 'message_copied') {
        showToast('Message copied to clipboard!');
    } else if (eventType === 'consent_requested') {
        showToast('Please review the consent request');
    }
};

// Custom toast notification system
let toastTimeout;
function showToast(message, duration = 3000) {
    // Clear any existing toast
    clearTimeout(toastTimeout);
    
    // Remove existing toast if any
    const existingToast = document.querySelector('.chat-toast');
    if (existingToast) {
        existingToast.remove();
    }
    
    // Create new toast
    const toast = document.createElement('div');
    toast.className = 'chat-toast';
    toast.textContent = message;
    toast.style.position = 'fixed';
    toast.style.bottom = '20px';
    toast.style.right = '20px';
    toast.style.backgroundColor = '#333';
    toast.style.color = '#fff';
    toast.style.padding = '10px 15px';
    toast.style.borderRadius = '4px';
    toast.style.zIndex = '9999';
    toast.style.opacity = '0';
    toast.style.transition = 'opacity 0.3s ease';
    
    // Add to document
    document.body.appendChild(toast);
    
    // Show toast
    setTimeout(() => {
        toast.style.opacity = '1';
    }, 10);
    
    // Hide and remove toast after duration
    toastTimeout = setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => {
            toast.remove();
        }, 300);
    }, duration);
}

// Resize textarea based on content
window.autoResizeTextarea = function(element) {
    if (element) {
        element.style.height = 'auto';
        element.style.height = (element.scrollHeight) + 'px';
    }
};