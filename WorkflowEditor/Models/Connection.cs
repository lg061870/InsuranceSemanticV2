using System;

namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Represents a connection between two ports
    /// </summary>
    public class Connection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string SourcePortId { get; set; } = string.Empty;
        public string TargetPortId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional condition that must be met for this connection to be active
        /// </summary>
        public string Condition { get; set; } = string.Empty;

        /// <summary>
        /// Whether the connection is currently selected
        /// </summary>
        public bool IsSelected { get; set; } = false;

        public Connection()
        {
        }

        public Connection(string sourcePortId, string targetPortId)
        {
            SourcePortId = sourcePortId;
            TargetPortId = targetPortId;
        }

        public Connection Clone()
        {
            return new Connection
            {
                Id = Id,
                Name = Name,
                SourcePortId = SourcePortId,
                TargetPortId = TargetPortId,
                Label = Label,
                Condition = Condition,
                IsSelected = IsSelected
            };
        }
    }
}