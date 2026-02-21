using System.ComponentModel.DataAnnotations;

namespace ConversaCore.BlazorTemplateHost.Topics.SampleTopic.Models;

/// <summary>
/// Simple model for the sample topic input card.
/// </summary>
public class SampleInputModel
{
    [Required]
    public string? Question { get; set; }
}
