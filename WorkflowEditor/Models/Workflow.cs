using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Represents a complete workflow with nodes and connections
    /// </summary>
    public class Workflow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Workflow";
        public string Description { get; set; } = string.Empty;
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<Connection> Connections { get; set; } = new List<Connection>();
        
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Adds a node to the workflow
        /// </summary>
        public void AddNode(Node node)
        {
            Nodes.Add(node);
            LastModified = DateTime.Now;
        }

        /// <summary>
        /// Removes a node from the workflow
        /// </summary>
        public void RemoveNode(string nodeId)
        {
            // Find all connections that involve this node's ports
            var nodePorts = Nodes.FirstOrDefault(n => n.Id == nodeId)?.Ports.Select(p => p.Id).ToList() ?? new List<string>();
            
            // Remove all connections connected to this node
            Connections.RemoveAll(c => 
                nodePorts.Contains(c.SourcePortId) || 
                nodePorts.Contains(c.TargetPortId));
            
            // Remove the node
            Nodes.RemoveAll(n => n.Id == nodeId);
            LastModified = DateTime.Now;
        }

        /// <summary>
        /// Adds a connection between two ports in the workflow
        /// </summary>
        public bool AddConnection(string sourcePortId, string targetPortId)
        {
            // Find the ports
            var sourcePort = FindPortById(sourcePortId);
            var targetPort = FindPortById(targetPortId);
            
            // Validate that the ports exist and are of the correct types
            if (sourcePort == null || targetPort == null ||
                sourcePort.Type != PortType.Output ||
                targetPort.Type != PortType.Input)
            {
                return false;
            }
            
            // Check if the connection already exists
            if (Connections.Any(c => c.SourcePortId == sourcePortId && c.TargetPortId == targetPortId))
            {
                return false;
            }
            
            // Check if the target port already has a connection and doesn't allow multiple
            if (!targetPort.AllowMultipleConnections && 
                Connections.Any(c => c.TargetPortId == targetPortId))
            {
                return false;
            }
            
            // Create and add the connection
            var connection = new Connection(sourcePortId, targetPortId);
            Connections.Add(connection);
            LastModified = DateTime.Now;
            
            return true;
        }

        /// <summary>
        /// Removes a connection from the workflow
        /// </summary>
        public void RemoveConnection(string connectionId)
        {
            Connections.RemoveAll(c => c.Id == connectionId);
            LastModified = DateTime.Now;
        }

        /// <summary>
        /// Finds a port by its ID
        /// </summary>
        public Port? FindPortById(string portId)
        {
            foreach (var node in Nodes)
            {
                var port = node.Ports.FirstOrDefault(p => p.Id == portId);
                if (port != null)
                {
                    return port;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Clears the workflow
        /// </summary>
        public void Clear()
        {
            Nodes.Clear();
            Connections.Clear();
            LastModified = DateTime.Now;
        }

        /// <summary>
        /// Creates a deep copy of the workflow
        /// </summary>
        public Workflow Clone()
        {
            var workflow = new Workflow
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Created = Created,
                LastModified = DateTime.Now
            };
            
            // Clone nodes
            foreach (var node in Nodes)
            {
                workflow.Nodes.Add(node.Clone());
            }
            
            // Clone connections
            foreach (var connection in Connections)
            {
                workflow.Connections.Add(connection.Clone());
            }
            
            return workflow;
        }
    }
}