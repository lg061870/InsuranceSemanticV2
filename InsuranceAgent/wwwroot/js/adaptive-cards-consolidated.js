// adaptive-cards-consolidated.js v1.1.1
(function () {
    console.log("Loading adaptive-cards-consolidated.js v1.1.1");

    // --- Ensure AdaptiveCards library is loaded (kept for compatibility) ---
    // Note: Your flow uses a custom renderer (window.AdaptiveCardRenderer),
    // so this loader is not required; retained to avoid breaking callers.
    window.ensureAdaptiveCardsLoaded = function () {
        return new Promise((resolve, reject) => {
            if (typeof AdaptiveCards !== "undefined") {
                console.log("AdaptiveCards already loaded");
                resolve(true);
                return;
            }

            console.log("Loading AdaptiveCards library...");
            const script = document.createElement("script");
            script.src = "https://unpkg.com/adaptivecards@latest/dist/adaptivecards.min.js";

            script.onload = () => {
                console.log("AdaptiveCards loaded from primary CDN");
                resolve(true);
            };

            script.onerror = () => {
                console.warn("Failed to load AdaptiveCards from primary CDN, trying alternative...");
                const altScript = document.createElement("script");
                altScript.src = "https://cdn.jsdelivr.net/npm/adaptivecards@latest/dist/adaptivecards.min.js";

                altScript.onload = () => {
                    console.log("AdaptiveCards loaded from alternative CDN");
                    resolve(true);
                };

                altScript.onerror = () => {
                    console.error("Failed to load AdaptiveCards from all sources");
                    reject(new Error("Failed to load AdaptiveCards library"));
                };

                document.head.appendChild(altScript);
            };

            document.head.appendChild(script);
        });
    };

    // --- Safe DOM removal (kept for compatibility) ---
    window.safeRemoveChild = function (parent, child) {
        if (!parent || !child) return false;
        if (typeof parent.contains !== "function") return false;

        if (parent.contains(child)) {
            try {
                parent.removeChild(child);
                return true;
            } catch (e) {
                console.warn("safeRemoveChild ignored missing child:", e);
            }
        }
        return false;
    };

    // --- Render AdaptiveCard via custom renderer ---
    // wrapperId: element that will contain a `.adaptive-card-host` div
    // cardJson:  stringified Adaptive Card object (or already-parsed object)
    // dotNetHelper: DotNetObjectReference to call back into Blazor (expects OnCardSubmit)
    window.renderAdaptiveCard = function (wrapperId, cardJson, dotNetHelper) {
        try {
            const wrapper = document.getElementById(wrapperId);
            if (!wrapper) {
                console.error(`[AdaptiveCards] Wrapper with ID ${wrapperId} not found`);
                return false;
            }

            // Ensure host div exists
            let host = wrapper.querySelector(".adaptive-card-host");
            if (!host) {
                host = document.createElement("div");
                host.className = "adaptive-card-host";
                wrapper.appendChild(host);
            }

            // Attach helper for downstream lookups (renderAction fallback, etc.)
            wrapper.dotNetHelper = dotNetHelper || null;

            // Clear host content
            host.innerHTML = "";

            // Use custom renderer instead of AdaptiveCards.js
            if (typeof window.AdaptiveCardRenderer === "undefined") {
                host.innerHTML = `<div class="adaptive-card-loading"><p>Loading custom card renderer...</p></div>`;
                console.error("[AdaptiveCards] AdaptiveCardRenderer is not available. Ensure js/custom-adaptive-card-renderer.js is loaded first.");
                return false;
            }

            // Accept object or string
            const cardObj = (typeof cardJson === "string")
                ? JSON.parse(cardJson)
                : (cardJson || {});

            // Defensive: basic shape check
            if (!cardObj || typeof cardObj !== "object") {
                console.error("[AdaptiveCards] Card JSON is not an object:", cardObj);
                return false;
            }

            // Render and bridge submit to Blazor
            window.AdaptiveCardRenderer.render(cardObj, host, function (submitData) {
                try {
                    if (dotNetHelper && typeof dotNetHelper.invokeMethodAsync === "function") {
                        console.debug("[AdaptiveCards] Invoking OnCardSubmit with payload:", submitData);
                        dotNetHelper.invokeMethodAsync("OnCardSubmit", submitData);
                    } else {
                        console.warn("[AdaptiveCards] dotNetHelper missing or invalid; submit payload:", submitData);
                    }
                } catch (invokeErr) {
                    console.error("[AdaptiveCards] Error invoking OnCardSubmit:", invokeErr);
                }
            });

            return true;
        } catch (error) {
            console.error("[AdaptiveCards] Error rendering custom adaptive card:", error);
            return false;
        }
    };


    // --- Extract Actions (helper for external toolbars, etc.) ---
    window.extractCardActions = function (cardJson) {
        try {
            const cardObj = (typeof cardJson === "string") ? JSON.parse(cardJson) : (cardJson || {});
            const actions = Array.isArray(cardObj.actions) ? cardObj.actions : [];
            return actions.map((a, i) => ({
                id: a.id || `action_${i}`,
                title: a.title || `Action ${i}`,
                data: a.data || {},
                style: a.style || "default",
                type: a.type || "Action.Submit"
            }));
        } catch (error) {
            console.error("Error extracting card actions:", error);
            return [];
        }
    };

    // --- Handle Submit Feedback (legacy passthrough kept for compatibility) ---
    window.handleCardSubmit = function (actionType, data) {
        console.log(`Handling card submit: ${actionType}`, data);
        // Fallback/manual path; generally not needed when renderer invokes Blazor directly.
        return data;
    };

    // --- Setup Adaptive Card Events (stores DotNet helper on wrapper for later use) ---
    window.setupAdaptiveCardEvents = function (wrapperId, dotNetHelper) {
        const wrapper = document.getElementById(wrapperId);
        if (!wrapper) {
            console.error(`Card wrapper with ID ${wrapperId} not found`);
            return;
        }
        wrapper.dotNetHelper = dotNetHelper || null;
        console.log(`Enhanced adaptive card events set up for ${wrapperId}`);
    };

    // --- Global API (stable surface) ---
    window.adaptiveCards = {
        init: function () {
            console.log("Enhanced adaptive cards initialized");
        },
        renderAdaptiveCard: window.renderAdaptiveCard,
        extractCardActions: window.extractCardActions,
        handleCardSubmit: window.handleCardSubmit,
        setupAdaptiveCardEvents: window.setupAdaptiveCardEvents
    };

    // --- Init when DOM ready ---
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => {
            if (window.adaptiveCards && typeof window.adaptiveCards.init === "function") {
                window.adaptiveCards.init();
            }
        });
    } else {
        if (window.adaptiveCards && typeof window.adaptiveCards.init === "function") {
            window.adaptiveCards.init();
        }
    }
})();
