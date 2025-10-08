using System.ComponentModel.DataAnnotations;

namespace ConversaCore.Validation {
    /// <summary>
    /// Validates that a ZIP code is a valid California ZIP if not empty.
    /// Note: Empty is allowed — RequiredIf will enforce separately.
    /// </summary>
    public class CaliforniaZipValidationAttribute : ValidationAttribute {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
            var str = value?.ToString();
            if (string.IsNullOrWhiteSpace(str)) {
                return ValidationResult.Success;
            }

            if (!int.TryParse(str, out var zip) || str.Length != 5) {
                return new ValidationResult(
                    "ZIP Code must be a 5-digit number.",
                    new[] { validationContext.MemberName ?? "<unknown>" }
                );
            }

            if (zip < 90001 || zip > 96162) {
                return new ValidationResult(
                    "ZIP Code must be within California (90001–96162).",
                    new[] { validationContext.MemberName ?? "<unknown>" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
