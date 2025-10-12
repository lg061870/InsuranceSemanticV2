using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace ConversaCore.Validation
{
    /// <summary>
    /// Validates that a choice (radio button or checkbox) has been selected.
    /// This attribute works with:
    /// 1. Nullable boolean properties (bool?) representing single radio buttons
    /// 2. String properties that represent radio button selections
    /// 3. Groups of radio buttons where only one should be selected (mutual exclusivity)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredChoiceAttribute : ValidationAttribute
    {
        private readonly string[] _otherPropertyNames;
        private readonly bool _requireMutualExclusion;
        
        /// <summary>
        /// Validates that a single choice has been made.
        /// </summary>
        public RequiredChoiceAttribute() : base("You must select an option.")
        {
            _otherPropertyNames = Array.Empty<string>();
            _requireMutualExclusion = false;
        }

        /// <summary>
        /// Validates that a single choice has been made.
        /// </summary>
        /// <param name="errorMessage">Custom error message</param>
        public RequiredChoiceAttribute(string errorMessage) : base(errorMessage)
        {
            _otherPropertyNames = Array.Empty<string>();
            _requireMutualExclusion = false;
        }

        /// <summary>
        /// Validates that one (and only one) option is selected from a group of radio buttons.
        /// </summary>
        /// <param name="requireMutualExclusion">If true, ensures only one option is selected</param>
        /// <param name="otherPropertyNames">Names of other properties in the radio group</param>
        public RequiredChoiceAttribute(bool requireMutualExclusion, params string[] otherPropertyNames) 
            : base("You must select exactly one option.")
        {
            _otherPropertyNames = otherPropertyNames ?? Array.Empty<string>();
            _requireMutualExclusion = requireMutualExclusion;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // For single property validation (no radio group)
            if (_otherPropertyNames.Length == 0)
            {
                return ValidateSingleProperty(value, validationContext);
            }
            
            // For radio groups with multiple properties
            return ValidateRadioGroup(value, validationContext);
        }

        private ValidationResult? ValidateSingleProperty(object? value, ValidationContext validationContext)
        {
            // For radio buttons, empty strings often get sent instead of nulls
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                return new ValidationResult(
                    ErrorMessage ?? $"You must select an option for {validationContext.DisplayName}.",
                    new[] { validationContext.MemberName ?? "<unknown>" }
                );
            }

            // For bool? values, we need an explicit true or false, not null
            if (value is bool?)
            {
                bool? boolValue = (bool?)value;
                if (!boolValue.HasValue)
                {
                    return new ValidationResult(
                        ErrorMessage ?? $"You must select an option for {validationContext.DisplayName}.",
                        new[] { validationContext.MemberName ?? "<unknown>" }
                    );
                }
            }

            // Success is a nullable reference type in newer .NET versions
            return ValidationResult.Success;
        }

        private ValidationResult? ValidateRadioGroup(object? value, ValidationContext validationContext)
        {
            var instance = validationContext.ObjectInstance;
            var type = instance.GetType();
            
            // Get the values of all properties in the group
            var allProperties = new List<string>();
            if (validationContext.MemberName != null)
                allProperties.Add(validationContext.MemberName);
            allProperties.AddRange(_otherPropertyNames);

            var selectedCount = 0;
            var members = new List<string>();

            // Check each property in the radio group
            foreach (var propName in allProperties)
            {
                if (propName == null) continue;
                
                var property = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null) continue;

                var propValue = property.GetValue(instance);
                
                // Count selected options
                if (propValue != null && IsSelected(propValue))
                {
                    selectedCount++;
                }

                members.Add(propName);
            }

            // No option selected
            if (selectedCount == 0)
            {
                return new ValidationResult(
                    ErrorMessage ?? "You must select an option.",
                    members.ToArray()
                );
            }

            // Multiple options selected but we require exclusivity
            if (_requireMutualExclusion && selectedCount > 1)
            {
                return new ValidationResult(
                    "Only one option should be selected.",
                    members.ToArray()
                );
            }

            return ValidationResult.Success;
        }

        private bool IsSelected(object value)
        {
            // Handle string values (often from toggle inputs)
            if (value is string stringVal)
            {
                return !string.IsNullOrEmpty(stringVal) && 
                       (stringVal.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        !stringVal.Equals("false", StringComparison.OrdinalIgnoreCase));
            }
            
            // Handle boolean values
            if (value is bool boolVal)
            {
                return boolVal;
            }
            
            // Handle nullable booleans with explicit cast
            if (value is bool?)
            {
                return ((bool?)value) == true;
            }
            
            return false;
        }
    }
}