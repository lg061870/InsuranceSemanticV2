using System;
using System.ComponentModel.DataAnnotations;
using ConversaCore.Cards;
using ConversaCore.Validation;

namespace InsuranceAgent.Topics;

public class RadioButtonDemoModel : BaseCardModel
{
    [RequiredChoice(ErrorMessage = "Please indicate whether you have insurance.")]
    public bool? HasInsurance { get; set; }

    [RequiredChoice(ErrorMessage = "Please indicate whether you own a home.")]
    public bool? HasHome { get; set; }

    [RequiredChoice(ErrorMessage = "Please indicate whether you own a car.")]
    public bool? HasCar { get; set; }

    /// <summary>
    /// Demonstrates validating that values are consistent across radio button groups.
    /// </summary>
    /// <returns>True if the model passes cross-field validation.</returns>
    public bool ValidateRadioConsistency()
    {
        // Example: If they have insurance, we expect some insurable assets
        if (HasInsurance == true && HasHome != true && HasCar != true)
        {
            // This could add a custom validation error
            return false;
        }
        
        return true;
    }
}