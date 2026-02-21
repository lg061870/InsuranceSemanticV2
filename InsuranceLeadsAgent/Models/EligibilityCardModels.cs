using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ConversaCore.Cards;

namespace InsuranceLeadsAgent.Models;

/// <summary>
/// Combined model for the single pre-qualification card used by
/// EligibilityTopic. BaseCardModel.UpdateContext will project these
/// properties into the TopicWorkflowContext.
/// </summary>
public class PreQualificationEligibilityModel : BaseCardModel {
    [Range(18, 85, ErrorMessage = "Please enter an age between 18 and 85.")]
    public int Age { get; set; }

    [Range(48, 84, ErrorMessage = "Height must be between 48 and 84 inches.")]
    public double Height { get; set; }

    [Range(80, 500, ErrorMessage = "Weight must be between 80 and 500 pounds.")]
    public double Weight { get; set; }

    public List<string> Conditions { get; set; } = new();
}
