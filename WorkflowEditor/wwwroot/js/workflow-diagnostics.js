// Diagnostic functions for workflow-editor

// Detect browser information
window.detectBrowser = function(elementId) {
    var element = document.getElementById(elementId);
    if (!element) return;
    
    var userAgent = navigator.userAgent;
    var browserName = "Unknown";
    var browserVersion = "";
    
    if (userAgent.match(/chrome|chromium|crios/i)) {
        browserName = "Chrome";
    } else if (userAgent.match(/firefox|fxios/i)) {
        browserName = "Firefox";
    } else if (userAgent.match(/safari/i)) {
        browserName = "Safari";
    } else if (userAgent.match(/edg/i)) {
        browserName = "Edge";
    } else if (userAgent.match(/opr\//i)) {
        browserName = "Opera";
    } else if (userAgent.match(/trident/i)) {
        browserName = "Internet Explorer";
    }
    
    // Try to get version
    var match = userAgent.match(/(chrome|chromium|safari|firefox|msie|trident|edge|opr(?=\/))\/?\s*(\d+)/i);
    if (match && match.length >= 3) {
        browserVersion = match[2];
    }
    
    element.textContent = `${browserName} ${browserVersion} (${navigator.platform})`;
};

// Check if Cytoscape.js is loaded
window.checkCytoscapeStatus = function(elementId) {
    var element = document.getElementById(elementId);
    if (!element) return;
    
    if (window.cytoscape) {
        element.textContent = "Loaded ✅ (version: " + window.cytoscape.version + ")";
        element.classList.add("text-success");
    } else {
        element.textContent = "Not Loaded ❌";
        element.classList.add("text-danger");
    }
};

// Check container status
window.checkContainerStatus = function(containerId, statusElementId) {
    var container = document.getElementById(containerId);
    var statusElement = document.getElementById(statusElementId);
    
    if (!statusElement) return;
    
    if (!container) {
        statusElement.textContent = "Container not found ❌";
        statusElement.classList.add("text-danger");
        return;
    }
    
    var width = container.offsetWidth;
    var height = container.offsetHeight;
    
    if (width === 0 || height === 0) {
        statusElement.textContent = `Container found but has zero dimensions: ${width}x${height} ⚠️`;
        statusElement.classList.add("text-warning");
    } else {
        statusElement.textContent = `Container found with dimensions: ${width}x${height} ✅`;
        statusElement.classList.add("text-success");
    }
};

// Initialize a test Cytoscape instance directly (without workflowCanvas)
window.initializeTestCytoscape = function(containerId) {
    var container = document.getElementById(containerId);
    if (!container) {
        console.error(`Container with ID '${containerId}' not found`);
        return false;
    }
    
    if (!window.cytoscape) {
        console.error('Cytoscape.js not loaded');
        return false;
    }
    
    try {
        var cy = cytoscape({
            container: container,
            elements: [
                // Test node
                { 
                    data: { id: 'test', label: 'Test Node' },
                    position: { x: 100, y: 100 }
                }
            ],
            style: [
                {
                    selector: 'node',
                    style: {
                        'background-color': '#6FB1FC',
                        'label': 'data(label)',
                        'text-valign': 'center',
                        'text-halign': 'center'
                    }
                }
            ]
        });
        
        return true;
    } catch (error) {
        console.error('Error initializing test Cytoscape instance:', error);
        return false;
    }
};