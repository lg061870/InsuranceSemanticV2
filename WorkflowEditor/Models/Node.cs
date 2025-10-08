using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Represents a node in the workflow
    /// </summary>
    public class Node
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public Position Position { get; set; } = new Position();
        public Size Size { get; set; } = new Size(150, 100);
        public List<Port> Ports { get; set; } = new List<Port>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;

        public Node()
        {
        }

        public Node(string name, NodeType type, Position position)
        {
            Name = name;
            Title = name;
            Type = type;
            Position = position;
            InitializePorts();
        }

        /// <summary>
        /// Initialize the default ports for this node based on its type
        /// </summary>
        public void InitializePorts()
        {
            Ports.Clear();
            
            // Create default ports based on node type
            switch (Type)
            {
                case NodeType.Trigger:
                    // Trigger nodes only have output ports
                    AddPort(new Port("Output", PortType.Output, Id));
                    break;
                case NodeType.Action:
                case NodeType.Variable:
                    // Action and Variable nodes have input and output ports
                    AddPort(new Port("Input", PortType.Input, Id));
                    AddPort(new Port("Output", PortType.Output, Id));
                    break;
                case NodeType.Condition:
                    // Condition nodes have one input and two outputs (true/false paths)
                    AddPort(new Port("Input", PortType.Input, Id));
                    AddPort(new Port("True", PortType.Output, Id));
                    AddPort(new Port("False", PortType.Output, Id));
                    break;
                case NodeType.Connector:
                    // Connector nodes can have multiple inputs and outputs
                    AddPort(new Port("Input", PortType.Input, Id) { AllowMultipleConnections = true });
                    AddPort(new Port("Output", PortType.Output, Id) { AllowMultipleConnections = true });
                    break;
                case NodeType.Loop:
                    // Loop nodes have input, output, and loop back ports
                    AddPort(new Port("Input", PortType.Input, Id));
                    AddPort(new Port("Output", PortType.Output, Id));
                    AddPort(new Port("Loop", PortType.Output, Id));
                    break;
                case NodeType.End:
                    // End nodes only have input ports
                    AddPort(new Port("Input", PortType.Input, Id) { AllowMultipleConnections = true });
                    break;
            }
        }

        public void AddPort(Port port)
        {
            port.NodeId = Id;
            Ports.Add(port);
            PositionPorts();
        }

        public void RemovePort(string portId)
        {
            Ports.RemoveAll(p => p.Id == portId);
            PositionPorts();
        }

        /// <summary>
        /// Positions the ports around the node
        /// </summary>
        public void PositionPorts()
        {
            var inputPorts = Ports.Where(p => p.Type == PortType.Input).ToList();
            var outputPorts = Ports.Where(p => p.Type == PortType.Output).ToList();
            
            // Position input ports on left side
            for (int i = 0; i < inputPorts.Count; i++)
            {
                double yPosition = ((i + 1.0) / (inputPorts.Count + 1)) * Size.Height;
                inputPorts[i].RelativePosition = new Position(0, yPosition);
            }
            
            // Position output ports on right side
            for (int i = 0; i < outputPorts.Count; i++)
            {
                double yPosition = ((i + 1.0) / (outputPorts.Count + 1)) * Size.Height;
                outputPorts[i].RelativePosition = new Position(Size.Width, yPosition);
            }
        }

        public Port? GetPortById(string portId)
        {
            return Ports.FirstOrDefault(p => p.Id == portId);
        }
        
        public Node Clone()
        {
            var clone = new Node
            {
                Id = Id,
                Name = Name,
                Title = Title,
                Type = Type,
                Position = Position.Clone(),
                Size = Size.Clone(),
                Properties = new Dictionary<string, object>(Properties),
                Description = Description,
                IsSelected = IsSelected
            };
            
            // Clone ports
            foreach (var port in Ports)
            {
                clone.Ports.Add(port.Clone());
            }
            
            return clone;
        }
    }
}