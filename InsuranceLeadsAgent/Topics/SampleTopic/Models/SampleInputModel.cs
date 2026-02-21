using System.ComponentModel.DataAnnotations;

namespace InsuranceLeadsAgent.Topics.SampleTopic.Models
{
    /// <summary>
    /// Simple model for the sample topic input card.
    /// </summary>
    public class SampleInputModel
    {
        [Required]
        public string? Question { get; set; }
    }
}
