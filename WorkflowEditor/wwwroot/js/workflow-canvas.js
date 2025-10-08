/**
 * workflow-canvas.js
 * JavaScript interop for the Workflow Editor using Cytoscape.js
 */

// Create a namespace for our workflow editor functions
window.workflowCanvas = (function () {
    // References to key elements
    let cy = null;
    let container = null;
    let dotNetHelper = null;
    let nodeTemplates = {};
    let isInitialized = false;
    
    // Global error handler for debugging
    window.addEventListener('error', function(event) {
        console.error('JavaScript Error:', event.message);
        console.error('Source:', event.filename);
        console.error('Line:', event.lineno);
        console.error('Column:', event.colno);
        console.error('Error object:', event.error);
        
        // Try to log to .NET console if dotNetHelper is available
        if (dotNetHelper) {
            try {
                dotNetHelper.invokeMethodAsync('OnJavaScriptError', 
                    `Error: ${event.message} at ${event.filename}:${event.lineno}:${event.colno}`);
            } catch (e) {
                console.error('Failed to log error to .NET:', e);
            }
        }
    });

    // Options for Cytoscape
    const DEFAULT_OPTIONS = {
        layout: {
            name: 'grid', // Default layout
            fit: true,
            padding: 50,
            rows: undefined,
            columns: undefined
        },
        style: [
            // Basic node styling
            {
                selector: 'node',
                style: {
                    'background-color': '#6FB1FC',
                    'border-width': 1,
                    'border-color': '#2B65EC',
                    'label': 'data(label)',
                    'text-valign': 'center',
                    'text-halign': 'center',
                    'color': '#000000',
                    'width': 'data(width)',
                    'height': 'data(height)',
                    'shape': 'round-rectangle'
                }
            },
            // Selected node styling
            {
                selector: 'node:selected',
                style: {
                    'border-width': 3,
                    'border-color': '#FF5733',
                    'box-shadow': '0 0 5px #FF5733'
                }
            },
            // Basic edge styling
            {
                selector: 'edge',
                style: {
                    'width': 2,
                    'line-color': '#2B65EC',
                    'target-arrow-color': '#2B65EC',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier',
                    'label': 'data(label)',
                    'color': '#000000',
                    'text-rotation': 'autorotate',
                    'text-background-color': '#FFFFFF',
                    'text-background-opacity': 0.8,
                    'text-background-shape': 'round-rectangle',
                    'text-background-padding': 3
                }
            },
            // Selected edge styling
            {
                selector: 'edge:selected',
                style: {
                    'width': 3,
                    'line-color': '#FF5733',
                    'target-arrow-color': '#FF5733'
                }
            },
            // Node type-specific styling will be added dynamically
        ]
    };

    /**
     * Initialize the Cytoscape canvas
     * @param {string} containerId - The ID of the container element
     * @param {object} helper - The .NET helper object
     */
    function initialize(containerId, helper) {
        try {
            console.log(`Initializing workflow canvas with container ID: ${containerId}`);
            
            if (isInitialized) {
                console.warn('Workflow canvas already initialized');
                if (helper && typeof helper.invokeMethodAsync === 'function') {
                    helper.invokeMethodAsync('OnCanvasInitialized');
                }
                return;
            }

            if (!containerId) {
                console.error('Container ID is null or empty');
                if (helper && typeof helper.invokeMethodAsync === 'function') {
                    helper.invokeMethodAsync('OnJavaScriptError', 'Container ID is null or empty');
                }
                return;
            }

            if (!helper) {
                console.error('DotNet helper object is null or undefined');
                return;
            }
            
            // Store the .NET helper object
            dotNetHelper = helper;

            // Debug information about the browser environment
            console.log('Browser info:', {
                userAgent: navigator.userAgent,
                platform: navigator.platform,
                language: navigator.language,
                cookieEnabled: navigator.cookieEnabled,
                windowSize: `${window.innerWidth}x${window.innerHeight}`,
                screenSize: `${window.screen.width}x${window.screen.height}`,
                devicePixelRatio: window.devicePixelRatio || 1
            });

            container = document.getElementById(containerId);
            if (!container) {
                console.error(`Container element with id '${containerId}' not found in DOM`);
                dotNetHelper.invokeMethodAsync('OnJavaScriptError', `Container element with id '${containerId}' not found in DOM`);
                
                // Log all elements with IDs for debugging
                const allElements = document.querySelectorAll('[id]');
                console.log('Available elements with IDs:');
                allElements.forEach(el => console.log(`- ${el.id}`));
                
                return;
            }
            
            console.log(`Found container element: ${container.tagName}, size: ${container.offsetWidth}x${container.offsetHeight}`);
            
            // Store the .NET helper object
            dotNetHelper = helper;
            
            // Verify Cytoscape is available
            if (!window.cytoscape) {
                console.error('Cytoscape library not available on window object');
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnJavaScriptError', 'Cytoscape library not available on window object. Make sure it is properly loaded in _Host.cshtml.');
                }
                return;
            }
            
            console.log('Cytoscape found, version:', window.cytoscape.version);
            
            // Check container dimensions before initializing
            if (container.offsetWidth === 0 || container.offsetHeight === 0) {
                console.warn(`Container has zero dimensions: ${container.offsetWidth}x${container.offsetHeight}`);
                // Force explicit dimensions and try again after a delay
                container.style.width = '100%';
                container.style.height = '600px'; // Fallback height - increased for visibility
                console.log(`Set explicit dimensions, now: ${container.offsetWidth}x${container.offsetHeight}`);
                
                // If still has zero dimensions, report the error
                if (container.offsetWidth === 0 || container.offsetHeight === 0) {
                    const errorMsg = `Container still has zero dimensions after setting explicit size. Check CSS and container visibility.`;
                    console.error(errorMsg);
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnJavaScriptError', errorMsg);
                    }
                    return;
                }
            }
            
            // Log all styles applied to the container for debugging
            const computedStyle = window.getComputedStyle(container);
            console.log('Container computed styles:', {
                width: computedStyle.width,
                height: computedStyle.height,
                display: computedStyle.display,
                position: computedStyle.position,
                visibility: computedStyle.visibility,
                overflow: computedStyle.overflow
            });
            
            try {
                // Check if Lodash is available (needed for extensions)
                if (!window._ || typeof window._.memoize !== 'function' || typeof window._.throttle !== 'function') {
                    console.warn('Lodash library not detected or missing required functions (memoize/throttle)');
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnJavaScriptError', 'Lodash library not detected. Some features may not work properly.');
                    }
                } else {
                    console.log('Lodash library detected, version:', window._.VERSION);
                }
                
                // Register extensions if available
                if (window.cytoscapeDagre) {
                    console.log('Registering cytoscape-dagre extension');
                    cytoscape.use(cytoscapeDagre);
                }
                
                if (window.cytoscapeGridGuide) {
                    console.log('Registering cytoscape-grid-guide extension');
                    cytoscape.use(cytoscapeGridGuide);
                }
                
                if (window.cytoscapeEdgehandles) {
                    console.log('Registering cytoscape-edgehandles extension');
                    cytoscape.use(cytoscapeEdgehandles);
                }
            } catch (extError) {
                console.warn('Error registering Cytoscape extensions:', extError);
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnJavaScriptError', `Error registering extensions: ${extError.message}`);
                }
                // Continue anyway - extensions are optional
            }
            
            // Initialize Cytoscape with default options
            cy = cytoscape({
                container: container,
                ...DEFAULT_OPTIONS,
                elements: [],
                // Disable text selection during drag
                userZoomingEnabled: true,
                userPanningEnabled: true,
                boxSelectionEnabled: true,
                selectionType: 'single',
                wheelSensitivity: 0.3
            });
            
            // Verify Cytoscape was initialized correctly
            if (!cy) {
                console.error('Cytoscape object was not created properly');
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnJavaScriptError', 'Cytoscape object was not created properly');
                }
                return;
            }
            
            // Add interaction handlers
            registerEventHandlers();
            
            isInitialized = true;
            console.log('Workflow canvas initialization complete');
            
            // Notify .NET that initialization completed successfully
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnCanvasInitialized');
            }
        } catch (error) {
            console.error('Critical error during canvas initialization:', error);
            if (helper && typeof helper.invokeMethodAsync === 'function') {
                helper.invokeMethodAsync('OnJavaScriptError', `Critical error: ${error.message}`);
            }
        }
    }

    /**
     * Helper function to load a script (kept for potential future use)
     */
    function loadScript(src) {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = src;
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    /**
     * Register event handlers for Cytoscape
     */
    function registerEventHandlers() {
        if (!cy) return;
        
        // Node selection
        cy.on('select', 'node', function(e) {
            const node = e.target;
            dotNetHelper.invokeMethodAsync('OnNodeSelected', node.id());
        });

        // Edge selection
        cy.on('select', 'edge', function(e) {
            const edge = e.target;
            dotNetHelper.invokeMethodAsync('OnEdgeSelected', edge.id());
        });

        // Canvas tap (background click)
        cy.on('tap', function(e) {
            if (e.target === cy) {
                // Background clicked, clear selection
                dotNetHelper.invokeMethodAsync('OnSelectionCleared');
            }
        });

        // Node position change (after drag)
        cy.on('position', 'node', function(e) {
            const node = e.target;
            dotNetHelper.invokeMethodAsync('OnNodePositionChanged', node.id(), node.position().x, node.position().y);
        });

        // Mouse wheel for zooming
        container.addEventListener('wheel', function(e) {
            const zoom = cy.zoom();
            dotNetHelper.invokeMethodAsync('OnZoomChanged', zoom);
        });
    }

    /**
     * Loads a workflow from a JSON object
     * @param {object} workflow - The workflow object
     */
    function loadWorkflow(workflow) {
        try {
            console.log('Loading workflow...');
            
            if (!cy) {
                console.error('Cannot load workflow: Cytoscape instance not initialized');
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnJavaScriptError', 'Cannot load workflow: Cytoscape instance not initialized');
                }
                return;
            }
            
            if (!workflow) {
                console.error('Cannot load workflow: workflow is null or undefined');
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnJavaScriptError', 'Cannot load workflow: workflow is null or undefined');
                }
                return;
            }
            
            // Log workflow structure for debugging
            console.log(`Workflow: ${workflow.name || 'Unnamed'}`);
            console.log(`Nodes: ${workflow.nodes?.length || 0}`);
            console.log(`Connections: ${workflow.connections?.length || 0}`);
            
            // Clear current elements
            cy.elements().remove();
            
            const elements = [];
            
            // Add nodes
            if (workflow.nodes && Array.isArray(workflow.nodes)) {
                workflow.nodes.forEach(node => {
                    try {
                        // Validate node properties
                        if (!node.id) {
                            console.warn('Node missing ID, generating one');
                            node.id = 'node-' + Math.random().toString(36).substr(2, 9);
                        }
                        
                        // Ensure position exists
                        if (!node.position) {
                            console.warn(`Node ${node.id} missing position, using default`);
                            node.position = { x: 100, y: 100 };
                        }
                        
                        // Ensure size exists
                        if (!node.size) {
                            console.warn(`Node ${node.id} missing size, using default`);
                            node.size = { width: 120, height: 80 };
                        }
                        
                        elements.push({
                            group: 'nodes',
                            data: {
                                id: node.id,
                                label: node.title || node.name || 'Node',
                                width: node.size.width || 120,
                                height: node.size.height || 80,
                                nodeType: node.type || 'default',
                                properties: node.properties || {}
                            },
                            position: {
                                x: node.position.x || 0,
                                y: node.position.y || 0
                            },
                            selected: node.isSelected || false
                        });
                        
                        // Store ports data for later use (will be rendered as part of the node)
                        if (node.ports && Array.isArray(node.ports)) {
                            node.ports.forEach(port => {
                                try {
                                    if (!port.id) {
                                        console.warn('Port missing ID, generating one');
                                        port.id = 'port-' + Math.random().toString(36).substr(2, 9);
                                    }
                                    
                                    // Ensure relative position exists
                                    if (!port.relativePosition) {
                                        console.warn(`Port ${port.id} missing relativePosition, using default`);
                                        port.relativePosition = { x: 0, y: 0 };
                                    }
                                    
                                    elements.push({
                                        group: 'nodes',
                                        data: {
                                            id: port.id,
                                            label: port.name || 'Port',
                                            parent: node.id,
                                            width: 10,
                                            height: 10,
                                            portType: port.type || 'default',
                                            nodeId: node.id,
                                            relX: port.relativePosition.x || 0,
                                            relY: port.relativePosition.y || 0
                                        },
                                        position: {
                                            x: (node.position.x || 0) + (port.relativePosition.x || 0),
                                            y: (node.position.y || 0) + (port.relativePosition.y || 0)
                                        },
                                        classes: 'port'
                                    });
                                } catch (portError) {
                                    console.error(`Error processing port in node ${node.id}:`, portError);
                                }
                            });
                        }
                    } catch (nodeError) {
                        console.error('Error processing node:', nodeError, node);
                    }
                });
            }
            
            // Add connections
            if (workflow.connections && Array.isArray(workflow.connections)) {
                workflow.connections.forEach(conn => {
                    try {
                        if (!conn.id) {
                            console.warn('Connection missing ID, generating one');
                            conn.id = 'conn-' + Math.random().toString(36).substr(2, 9);
                        }
                        
                        if (!conn.sourcePortId || !conn.targetPortId) {
                            console.warn(`Connection ${conn.id} missing source or target port ID, skipping`);
                            return;
                        }
                        
                        elements.push({
                            group: 'edges',
                            data: {
                                id: conn.id,
                                label: conn.label || '',
                                source: conn.sourcePortId,
                                target: conn.targetPortId
                            }
                        });
                    } catch (connError) {
                        console.error('Error processing connection:', connError, conn);
                    }
                });
            }
            
            // Add elements to the graph
            if (elements.length > 0) {
                console.log(`Adding ${elements.length} elements to graph`);
                cy.add(elements);
                
                // Apply layout if there are nodes
                if (workflow.nodes && workflow.nodes.length > 0) {
                    console.log('Applying layout');
                    applyLayout('dagre');
                }
                
                // Fit the graph
                console.log('Fitting graph to view');
                cy.fit();
            } else {
                console.warn('No elements to add to graph');
            }
            
            console.log('Workflow loaded successfully');
        } catch (error) {
            console.error('Error loading workflow:', error);
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('OnJavaScriptError', `Error loading workflow: ${error.message}`);
            }
        }
    }

    /**
     * Applies a layout to the graph
     * @param {string} layoutName - The name of the layout to apply
     */
    function applyLayout(layoutName) {
        if (!cy) return;
        
        const layouts = {
            'grid': {
                name: 'grid',
                fit: true,
                padding: 50,
                rows: undefined,
                columns: undefined
            },
            'dagre': {
                name: 'dagre',
                rankDir: 'TB', // Top to Bottom
                rankSep: 100,  // Distance between ranks
                nodeSep: 50,   // Distance between nodes in the same rank
                padding: 50,
                fit: true,
                animate: true,
                animationDuration: 500
            },
            'circle': {
                name: 'circle',
                fit: true,
                padding: 50,
                animate: true,
                animationDuration: 500
            }
        };
        
        const layout = layouts[layoutName] || layouts['grid'];
        cy.layout(layout).run();
    }

    /**
     * Add a node to the graph
     * @param {object} node - The node object
     */
    function addNode(node) {
        if (!cy) return;
        
        // Add the node
        cy.add({
            group: 'nodes',
            data: {
                id: node.id,
                label: node.title || node.name,
                width: node.size.width,
                height: node.size.height,
                nodeType: node.type,
                properties: node.properties
            },
            position: {
                x: node.position.x,
                y: node.position.y
            }
        });
        
        // Add its ports
        if (node.ports) {
            node.ports.forEach(port => {
                cy.add({
                    group: 'nodes',
                    data: {
                        id: port.id,
                        label: port.name,
                        parent: node.id,
                        width: 10,
                        height: 10,
                        portType: port.type,
                        nodeId: node.id
                    },
                    position: {
                        x: node.position.x + port.relativePosition.x,
                        y: node.position.y + port.relativePosition.y
                    },
                    classes: 'port'
                });
            });
        }
    }

    /**
     * Remove a node from the graph
     * @param {string} nodeId - The ID of the node to remove
     */
    function removeNode(nodeId) {
        if (!cy) return;
        
        cy.getElementById(nodeId).remove();
    }

    /**
     * Add a connection between two ports
     * @param {object} connection - The connection object
     */
    function addConnection(connection) {
        if (!cy) return;
        
        cy.add({
            group: 'edges',
            data: {
                id: connection.id,
                label: connection.label,
                source: connection.sourcePortId,
                target: connection.targetPortId
            }
        });
    }

    /**
     * Remove a connection
     * @param {string} connectionId - The ID of the connection to remove
     */
    function removeConnection(connectionId) {
        if (!cy) return;
        
        cy.getElementById(connectionId).remove();
    }

    /**
     * Update the position of a node
     * @param {string} nodeId - The ID of the node
     * @param {number} x - The x-coordinate
     * @param {number} y - The y-coordinate
     */
    function updateNodePosition(nodeId, x, y) {
        if (!cy) return;
        
        const node = cy.getElementById(nodeId);
        if (node.length > 0) {
            node.position({ x, y });
            
            // Update positions of ports
            const ports = cy.nodes(`[nodeId="${nodeId}"]`);
            ports.forEach(port => {
                const relX = port.data('relX') || 0;
                const relY = port.data('relY') || 0;
                port.position({ x: x + relX, y: y + relY });
            });
        }
    }

    /**
     * Select a node
     * @param {string} nodeId - The ID of the node to select
     */
    function selectNode(nodeId) {
        if (!cy) return;
        
        cy.elements().unselect();
        cy.getElementById(nodeId).select();
    }

    /**
     * Select a connection
     * @param {string} connectionId - The ID of the connection to select
     */
    function selectConnection(connectionId) {
        if (!cy) return;
        
        cy.elements().unselect();
        cy.getElementById(connectionId).select();
    }

    /**
     * Clear all selections
     */
    function clearSelection() {
        if (!cy) return;
        
        cy.elements().unselect();
    }

    /**
     * Set the zoom level
     * @param {number} level - The zoom level
     */
    function setZoom(level) {
        if (!cy) return;
        
        cy.zoom(level);
    }

    /**
     * Get the current zoom level
     * @returns {number} The current zoom level
     */
    function getZoom() {
        if (!cy) return 1;
        
        return cy.zoom();
    }

    /**
     * Set the canvas position (pan)
     * @param {number} x - The x-coordinate
     * @param {number} y - The y-coordinate
     */
    function setPosition(x, y) {
        if (!cy) return;
        
        cy.pan({ x, y });
    }

    /**
     * Get the canvas position (pan)
     * @returns {object} The canvas position
     */
    function getPosition() {
        if (!cy) return { x: 0, y: 0 };
        
        const pan = cy.pan();
        return { x: pan.x, y: pan.y };
    }

    /**
     * Center the view on a specific node
     * @param {string} nodeId - The ID of the node
     */
    function centerOnNode(nodeId) {
        if (!cy) return;
        
        const node = cy.getElementById(nodeId);
        if (node.length > 0) {
            cy.animate({
                center: { eles: node },
                duration: 500
            });
        }
    }

    /**
     * Fit all elements in the view
     */
    function fitContent() {
        if (!cy) return;
        
        cy.fit();
    }

    /**
     * Get the position of the cursor on the canvas
     * @returns {object} The cursor position
     */
    function getCursorPosition() {
        if (!cy) return { x: 0, y: 0 };
        
        // Get the position of the last mouse event
        const event = cy.scratch('_lastMouseEvent');
        if (!event) {
            return { x: 0, y: 0 };
        }
        
        // Convert client coordinates to canvas coordinates
        const position = cy.projectIntoViewport(event.clientX, event.clientY);
        return { x: position[0], y: position[1] };
    }

    /**
     * Convert client coordinates to canvas coordinates
     * @param {number} clientX - The client x-coordinate
     * @param {number} clientY - The client y-coordinate
     * @returns {object} The canvas position
     */
    function clientToCanvasPosition(clientX, clientY) {
        if (!cy) return { x: 0, y: 0 };
        
        const position = cy.projectIntoViewport(clientX, clientY);
        return { x: position[0], y: position[1] };
    }

    // Register mouse move event to track cursor position
    document.addEventListener('mousemove', function(e) {
        if (cy) {
            cy.scratch('_lastMouseEvent', e);
        }
    });

    // Public API
    return {
        initialize,
        loadWorkflow,
        applyLayout,
        addNode,
        removeNode,
        addConnection,
        removeConnection,
        updateNodePosition,
        selectNode,
        selectConnection,
        clearSelection,
        setZoom,
        getZoom,
        setPosition,
        getPosition,
        centerOnNode,
        fitContent,
        getCursorPosition,
        clientToCanvasPosition
    };
})();