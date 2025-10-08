using System;

namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Defines the type of a port: input or output
    /// </summary>
    public enum PortType
    {
        Input,
        Output
    }

    /// <summary>
    /// Represents a connection point (port) on a node
    /// </summary>
    public class Port
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public PortType Type { get; set; }
        public Position Position { get; set; } = new Position();
        public string NodeId { get; set; } = string.Empty;
        public bool AllowMultipleConnections { get; set; } = false;
        
        /// <summary>
        /// Position relative to the parent node
        /// </summary>
        public Position RelativePosition { get; set; } = new Position();

        public Port()
        {
        }

        public Port(string name, PortType type, string nodeId)
        {
            Name = name;
            Type = type;
            NodeId = nodeId;
        }

        public Port Clone()
        {
            return new Port
            {
                Id = Id,
                Name = Name,
                Type = Type,
                Position = Position.Clone(),
                NodeId = NodeId,
                RelativePosition = RelativePosition.Clone(),
                AllowMultipleConnections = AllowMultipleConnections
            };
        }
    }
}