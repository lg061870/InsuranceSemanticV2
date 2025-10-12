using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Reflection;

namespace InsuranceAgent.Topics.CaliforniaResidentTopic
{
    /// <summary>
    /// Helper class to debug validation issues
    /// </summary>
    public static class ValidationDebuggingHelper
    {
        /// <summary>
        /// Runs validation on an object and returns detailed information about
        /// the validation results for debugging purposes.
        /// </summary>
        public static List<string> ValidateWithDetails<T>(T model)
        {
            var results = new List<string>();
            
            try
            {
                results.Add($"Validating model of type {typeof(T).FullName}");
                
                // Get all properties with validation attributes
                foreach (var property in typeof(T).GetProperties())
                {
                    var attributes = property.GetCustomAttributes(typeof(ValidationAttribute), true);
                    if (attributes.Length > 0)
                    {
                        var value = property.GetValue(model);
                        results.Add($"Property '{property.Name}' = '{value}', has {attributes.Length} validation attributes:");
                        
                        foreach (ValidationAttribute attr in attributes)
                        {
                            try
                            {
                                // Create validation context for this property
                                var context = new ValidationContext(model) 
                                { 
                                    MemberName = property.Name
                                };
                                
                                var isValid = attr.IsValid(value);
                                var result = isValid ? "Valid" : "INVALID";
                                
                                if (isValid)
                                {
                                    results.Add($"  ✅ {attr.GetType().Name}: {result}");
                                }
                                else
                                {
                                    var errorMsg = attr.FormatErrorMessage(property.Name);
                                    results.Add($"  ❌ {attr.GetType().Name}: {result} - {errorMsg}");
                                }
                            }
                            catch (Exception ex)
                            {
                                results.Add($"  ❌ Exception validating {attr.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        results.Add($"Property '{property.Name}' has no validation attributes");
                    }
                }
            
                // Run full validation
                var validationContext = new ValidationContext(model);
                var validationResults = new List<ValidationResult>();
                bool isModelValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
                
                results.Add($"Full model validation result: {(isModelValid ? "Valid" : "INVALID")}");
                if (!isModelValid)
                {
                    foreach (var error in validationResults)
                    {
                        results.Add($"Validation error: {error.ErrorMessage} (Properties: {string.Join(", ", error.MemberNames)})");
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add($"Exception during validation: {ex}");
            }
            
            return results;
        }
    }
}