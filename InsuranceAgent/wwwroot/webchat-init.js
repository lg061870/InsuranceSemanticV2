// TopicFlow Chat initialization script

/**
 * This script initializes the necessary components for the TopicFlow Chat Window.
 * Removed previous Copilot Studio/Bot Framework Web Chat specific code.
 */

document.addEventListener('DOMContentLoaded', function() {
    // Initialize AdaptiveCards
    if (typeof AdaptiveCards !== 'undefined') {
        console.log('Initializing adaptive cards');
        if (window.adaptiveCards && typeof window.adaptiveCards.init === 'function') {
            window.adaptiveCards.init();
        }
    } else {
        console.log('AdaptiveCards library not loaded from CDN. Loading local fallback...');
    }

    // Store AdaptiveCards initialization function for potential delayed loading
    window.adaptiveCardsDebug = {
        init: function() {
            console.log('Initializing adaptive cards');
            if (window.adaptiveCards && typeof window.adaptiveCards.init === 'function') {
                window.adaptiveCards.init();
            }
        }
    };
    
    // Initialize event listeners for TopicFlow events
    initializeTopicFlowEventHandlers();
});

/**
 * Sets up event handlers for TopicFlow system events
 */
function initializeTopicFlowEventHandlers() {
    // Listen for custom events from the TopicFlow system
    document.addEventListener('topicflow:event', function(event) {
        const eventData = event.detail;
        console.log('TopicFlow event received:', eventData.type, eventData);
        
        // Handle different event types
        switch (eventData.type) {
            case 'consent_requested':
                console.log('Consent requested by TopicFlow');
                // Handle consent request event
                if (window.homeComponentReference) {
                    // Trigger consent flow in UI
                }
                break;
                
            case 'questionnaire_requested':
                console.log('Questionnaire requested by TopicFlow');
                // Handle questionnaire request event
                if (window.homeComponentReference) {
                    // Trigger questionnaire flow in UI
                }
                break;
                
            case 'adaptive_card':
                console.log('Adaptive card received:', eventData.content);
                // Handle adaptive card rendering
                break;
                
            default:
                console.log('Unhandled TopicFlow event type:', eventData.type);
        }
    });
}