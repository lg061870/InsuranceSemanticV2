using Microsoft.JSInterop;
using System.Threading.Tasks;
using WorkflowEditorLib.Models;

namespace WorkflowEditorLib.Services
{
    /// <summary>
    /// Service for JavaScript interop functions related to the workflow canvas
    /// </summary>
    public class CanvasJsInterop
    {
        private readonly IJSRuntime _jsRuntime;
        
        public CanvasJsInterop(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }
        
        /// <summary>
        /// Initializes the canvas with the given element ID
        /// </summary>
        public async Task InitializeCanvas(string canvasElementId)
        {
            await _jsRuntime.InvokeVoidAsync("workflowCanvas.initialize", canvasElementId);
        }
        
        /// <summary>
        /// Sets the current zoom level of the canvas
        /// </summary>
        public async Task SetZoom(double zoomLevel)
        {
            await _jsRuntime.InvokeVoidAsync("workflowCanvas.setZoom", zoomLevel);
        }
        
        /// <summary>
        /// Gets the current zoom level of the canvas
        /// </summary>
        public async Task<double> GetZoom()
        {
            return await _jsRuntime.InvokeAsync<double>("workflowCanvas.getZoom");
        }
        
        /// <summary>
        /// Sets the canvas position (pan)
        /// </summary>
        public async Task SetCanvasPosition(double x, double y)
        {
            await _jsRuntime.InvokeVoidAsync("workflowCanvas.setPosition", x, y);
        }
        
        /// <summary>
        /// Gets the canvas position (pan)
        /// </summary>
        public async Task<Position> GetCanvasPosition()
        {
            return await _jsRuntime.InvokeAsync<Position>("workflowCanvas.getPosition");
        }
        
        /// <summary>
        /// Centers the view on the specified node
        /// </summary>
        public async Task CenterOnNode(string nodeId)
        {
            await _jsRuntime.InvokeVoidAsync("workflowCanvas.centerOnNode", nodeId);
        }
        
        /// <summary>
        /// Fits all nodes in the view
        /// </summary>
        public async Task FitContent()
        {
            await _jsRuntime.InvokeVoidAsync("workflowCanvas.fitContent");
        }
        
        /// <summary>
        /// Updates the position of a node on the canvas
        /// </summary>
        public async Task UpdateNodePosition(string nodeId, double x, double y)
        {
            await _jsRuntime.InvokeVoidAsync("workflowCanvas.updateNodePosition", nodeId, x, y);
        }
        
        /// <summary>
        /// Gets the current cursor position on the canvas
        /// </summary>
        public async Task<Position> GetCursorPosition()
        {
            return await _jsRuntime.InvokeAsync<Position>("workflowCanvas.getCursorPosition");
        }
        
        /// <summary>
        /// Gets the position on the canvas from a client point
        /// </summary>
        public async Task<Position> ClientToCanvasPosition(double clientX, double clientY)
        {
            return await _jsRuntime.InvokeAsync<Position>("workflowCanvas.clientToCanvasPosition", clientX, clientY);
        }
    }
}