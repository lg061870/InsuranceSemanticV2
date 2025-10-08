using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ConversaCore.Validation {
    /// <summary>
    /// Makes a property required if another property has a specific value.
    /// Example:
    /// [RequiredIf("IsCaliforniaResident", true, ErrorMessage = "ZIP Code is required.")]
    /// public string? ZipCode { get; set; }
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class RequiredIfAttribute : ValidationAttribute {
        private readonly string _otherPropertyName;
        private readonly object _targetValue;

        public RequiredIfAttribute(string otherPropertyName, object targetValue) {
            _otherPropertyName = otherPropertyName;
            _targetValue = targetValue;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
            var instance = validationContext.ObjectInstance;
            var type = instance.GetType();

            var otherProperty = type.GetProperty(_otherPropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (otherProperty == null) {
                return new ValidationResult(
                    $"Unknown property: {_otherPropertyName}",
                    new[] { validationContext.MemberName ?? "<unknown>" }
                );
            }

            var otherValue = otherProperty.GetValue(instance);

            if (Equals(otherValue, _targetValue)) {
                if (value == null || (value is string s && string.IsNullOrWhiteSpace(s))) {
                    return new ValidationResult(
                        ErrorMessage ?? $"{validationContext.DisplayName} is required.",
                        new[] { validationContext.MemberName ?? "<unknown>" }
                    );
                }
            }

            return ValidationResult.Success;
        }
    }
}
