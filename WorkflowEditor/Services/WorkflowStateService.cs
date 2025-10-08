using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using WorkflowEditorLib.Models;

namespace WorkflowEditorLib.Services
{
    /// <summary>
    /// Service for managing the state of the workflow
    /// </summary>
    public class WorkflowStateService
    {
        // Current workflow being edited
        public Workflow CurrentWorkflow { get; private set; } = new Workflow();
        
        // Variable store for the workflow
        public VariableStore VariableStore { get; private set; } = new VariableStore();
        
        // Currently selected node
        public Node? SelectedNode { get; private set; }
        
        // Currently selected connection
        public Connection? SelectedConnection { get; private set; }
        
        // Canvas state
        public double ZoomLevel { get; private set; } = 1.0;
        public Position CanvasPosition { get; private set; } = new Position(0, 0);
        
        // Event handlers
        public event Func<Task>? OnWorkflowChanged;
        public event Func<Node, Task>? OnNodeSelected;
        public event Func<Connection, Task>? OnConnectionSelected;
        public event Func<Task>? OnSelectionCleared;
        
        // History for undo/redo
        private readonly List<Workflow> _undoHistory = new List<Workflow>();
        private readonly List<Workflow> _redoHistory = new List<Workflow>();
        private const int MaxHistorySize = 50;
        
        /// <summary>
        /// Creates a new workflow
        /// </summary>
        public async Task CreateNewWorkflow()
        {
            SaveStateToHistory();
            CurrentWorkflow = new Workflow();
            VariableStore = new VariableStore();
            ClearSelection();
            await NotifyWorkflowChanged();
        }
        
        /// <summary>
        /// Loads a workflow
        /// </summary>
        public async Task LoadWorkflow(Workflow workflow)
        {
            SaveStateToHistory();
            CurrentWorkflow = workflow;
            ClearSelection();
            await NotifyWorkflowChanged();
        }
        
        /// <summary>
        /// Adds a node to the workflow
        /// </summary>
        public async Task AddNode(Node node)
        {
            SaveStateToHistory();
            CurrentWorkflow.AddNode(node);
            await NotifyWorkflowChanged();
        }
        
        /// <summary>
        /// Removes a node from the workflow
        /// </summary>
        public async Task RemoveNode(string nodeId)
        {
            SaveStateToHistory();
            CurrentWorkflow.RemoveNode(nodeId);
            
            // If the selected node was removed, clear the selection
            if (SelectedNode?.Id == nodeId)
            {
                ClearSelection();
            }
            
            await NotifyWorkflowChanged();
        }
        
        /// <summary>
        /// Updates a node in the workflow
        /// </summary>
        public async Task UpdateNode(Node node)
        {
            SaveStateToHistory();
            var existingNode = CurrentWorkflow.Nodes.Find(n => n.Id == node.Id);
            if (existingNode != null)
            {
                var index = CurrentWorkflow.Nodes.IndexOf(existingNode);
                CurrentWorkflow.Nodes[index] = node;
                
                // Update selected node if it's the same one
                if (SelectedNode?.Id == node.Id)
                {
                    SelectedNode = node;
                }
                
                await NotifyWorkflowChanged();
            }
        }
        
        /// <summary>
        /// Updates a node's position in the workflow
        /// </summary>
        public async Task UpdateNodePosition(string nodeId, Position position)
        {
            var node = CurrentWorkflow.Nodes.Find(n => n.Id == nodeId);
            if (node != null)
            {
                SaveStateToHistory();
                node.Position = position;
                await NotifyWorkflowChanged();
            }
        }
        
        /// <summary>
        /// Adds a connection between two ports in the workflow
        /// </summary>
        public async Task<bool> AddConnection(string sourcePortId, string targetPortId)
        {
            SaveStateToHistory();
            bool success = CurrentWorkflow.AddConnection(sourcePortId, targetPortId);
            if (success)
            {
                await NotifyWorkflowChanged();
            }
            return success;
        }
        
        /// <summary>
        /// Removes a connection from the workflow
        /// </summary>
        public async Task RemoveConnection(string connectionId)
        {
            SaveStateToHistory();
            CurrentWorkflow.RemoveConnection(connectionId);
            
            // If the selected connection was removed, clear the selection
            if (SelectedConnection?.Id == connectionId)
            {
                ClearSelection();
            }
            
            await NotifyWorkflowChanged();
        }
        
        /// <summary>
        /// Selects a node in the workflow
        /// </summary>
        public async Task SelectNode(string nodeId)
        {
            ClearSelection();
            
            SelectedNode = CurrentWorkflow.Nodes.Find(n => n.Id == nodeId);
            if (SelectedNode != null)
            {
                SelectedNode.IsSelected = true;
                if (OnNodeSelected != null)
                {
                    await OnNodeSelected.Invoke(SelectedNode);
                }
            }
        }
        
        /// <summary>
        /// Selects a connection in the workflow
        /// </summary>
        public async Task SelectConnection(string connectionId)
        {
            ClearSelection();
            
            SelectedConnection = CurrentWorkflow.Connections.Find(c => c.Id == connectionId);
            if (SelectedConnection != null && OnConnectionSelected != null)
            {
                await OnConnectionSelected.Invoke(SelectedConnection);
            }
        }
        
        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            if (SelectedNode != null)
            {
                SelectedNode.IsSelected = false;
                SelectedNode = null;
            }
            
            SelectedConnection = null;
            
            OnSelectionCleared?.Invoke();
        }
        
        /// <summary>
        /// Sets the zoom level of the canvas
        /// </summary>
        public void SetZoomLevel(double zoomLevel)
        {
            ZoomLevel = Math.Clamp(zoomLevel, 0.1, 3.0);
        }
        
        /// <summary>
        /// Sets the canvas position
        /// </summary>
        public void SetCanvasPosition(Position position)
        {
            CanvasPosition = position;
        }
        
        /// <summary>
        /// Saves the current state to the undo history
        /// </summary>
        private void SaveStateToHistory()
        {
            _undoHistory.Add(CurrentWorkflow.Clone());
            if (_undoHistory.Count > MaxHistorySize)
            {
                _undoHistory.RemoveAt(0);
            }
            _redoHistory.Clear();
        }
        
        /// <summary>
        /// Undoes the last action
        /// </summary>
        public async Task Undo()
        {
            if (_undoHistory.Count > 0)
            {
                // Save current state to redo history
                _redoHistory.Add(CurrentWorkflow.Clone());
                
                // Pop the last state from undo history
                var lastIndex = _undoHistory.Count - 1;
                CurrentWorkflow = _undoHistory[lastIndex];
                _undoHistory.RemoveAt(lastIndex);
                
                ClearSelection();
                await NotifyWorkflowChanged();
            }
        }
        
        /// <summary>
        /// Redoes the last undone action
        /// </summary>
        public async Task Redo()
        {
            if (_redoHistory.Count > 0)
            {
                // Save current state to undo history
                _undoHistory.Add(CurrentWorkflow.Clone());
                
                // Pop the last state from redo history
                var lastIndex = _redoHistory.Count - 1;
                CurrentWorkflow = _redoHistory[lastIndex];
                _redoHistory.RemoveAt(lastIndex);
                
                ClearSelection();
                await NotifyWorkflowChanged();
            }
        }
        
        /// <summary>
        /// Notifies that the workflow has changed
        /// </summary>
        public async Task NotifyWorkflowChanged()
        {
            if (OnWorkflowChanged != null)
            {
                await OnWorkflowChanged.Invoke();
            }
        }
    }
}