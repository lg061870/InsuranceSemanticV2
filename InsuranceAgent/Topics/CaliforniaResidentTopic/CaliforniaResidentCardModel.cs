using System.ComponentModel.DataAnnotations;
using ConversaCore.Validation;
using ConversaCore.Cards;

public class CaliforniaResidentModel : BaseCardModel {
    [Required(ErrorMessage = "You must confirm whether you are a California resident.")]
    public bool? IsCaliforniaResident { get; set; }

    [RequiredIf("IsCaliforniaResident", true, ErrorMessage = "ZIP Code is required for CA residents.")]
    [CaliforniaZipValidation] // handles both format + range if not blank
    public string? ZipCode { get; set; }
}
