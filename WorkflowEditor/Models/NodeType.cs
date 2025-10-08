using System;

namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Defines the different types of nodes available in the workflow editor
    /// </summary>
    public enum NodeType
    {
        Trigger,
        Action,
        Condition,
        Connector,
        Variable,
        Loop,
        End
    }
}