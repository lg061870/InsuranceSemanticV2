using System;
using System.Collections.Generic;

namespace WorkflowEditorLib.Models
{
    /// <summary>
    /// Defines the different data types for variables in the workflow
    /// </summary>
    public enum VariableType
    {
        String,
        Number,
        Boolean,
        DateTime,
        Object,
        Array
    }

    /// <summary>
    /// Represents a variable in the workflow
    /// </summary>
    public class Variable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public VariableType Type { get; set; } = VariableType.String;
        public object? Value { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsGlobal { get; set; } = false;
        
        public Variable Clone()
        {
            return new Variable
            {
                Id = Id,
                Name = Name,
                Type = Type,
                Value = Value,
                Description = Description,
                IsGlobal = IsGlobal
            };
        }
    }

    /// <summary>
    /// A collection of variables used in the workflow
    /// </summary>
    public class VariableStore
    {
        public List<Variable> Variables { get; set; } = new List<Variable>();

        public void AddVariable(Variable variable)
        {
            Variables.Add(variable);
        }

        public void RemoveVariable(string variableId)
        {
            Variables.RemoveAll(v => v.Id == variableId);
        }

        public Variable? GetVariable(string variableId)
        {
            return Variables.Find(v => v.Id == variableId);
        }

        public Variable? GetVariableByName(string name)
        {
            return Variables.Find(v => v.Name == name);
        }

        public void Clear()
        {
            Variables.Clear();
        }
    }
}