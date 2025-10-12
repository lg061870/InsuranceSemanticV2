using System;
using System.ComponentModel.DataAnnotations;
using ConversaCore.Validation;
using ConversaCore.Cards;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.CaliforniaResidentTopic;

public class CaliforniaResidentModel : BaseCardModel {
    [RequiredChoice(ErrorMessage = "You must confirm whether you are a California resident.")]
    public bool? IsCaliforniaResident { get; set; }

    [RequiredIf("IsCaliforniaResident", true, ErrorMessage = "ZIP Code is required for CA residents.")]
    [CaliforniaZipValidation] // handles both format + range if not blank
    public string? ZipCode { get; set; }
  
    /// <summary>
    /// Determines if the model contains a valid California ZIP code.
    /// </summary>
    /// <returns>True if the model contains a valid California ZIP code, false otherwise.</returns>
    public bool HasValidCaliforniaZip() {
        // If not a resident, we don't need to validate
        if (IsCaliforniaResident != true) {
            return false;
        }

        // ZIP must be present for a CA resident
        if (string.IsNullOrWhiteSpace(ZipCode)) {
            return false;
        }

        // ZIP must be numeric and 5 digits
        if (!int.TryParse(ZipCode, out var zip) || ZipCode.Length != 5) {
            return false;
        }

        // ZIP must be in California range (90001-96162)
        return zip >= 90001 && zip <= 96162;
    }
}
