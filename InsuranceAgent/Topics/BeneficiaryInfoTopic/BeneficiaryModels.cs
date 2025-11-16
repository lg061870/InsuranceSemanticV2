using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ConversaCore.Cards;

namespace InsuranceAgent.Topics; 
/// <summary>
/// Model for beneficiary information
/// </summary>
public class BeneficiaryInfoModel : BaseCardModel {
    /// <summary>
    /// Gets or sets the primary beneficiary's full name
    /// </summary>
    [Required(ErrorMessage = "Beneficiary name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [JsonPropertyName("beneficiary_name")]
    public string? BeneficiaryName { get; set; }

    /// <summary>
    /// Gets or sets the primary beneficiary's relationship to the insured
    /// </summary>
    [Required(ErrorMessage = "Relationship is required")]
    [JsonPropertyName("beneficiary_relation")]
    public string? BeneficiaryRelationship { get; set; }

    /// <summary>
    /// Gets or sets the primary beneficiary's date of birth
    /// </summary>
    [Required(ErrorMessage = "Date of birth is required")]
    [JsonPropertyName("beneficiary_dob")]
    public DateTime? BeneficiaryDob { get; set; }

    /// <summary>
    /// Gets or sets the primary beneficiary's percentage
    /// </summary>
    [Range(1, 100, ErrorMessage = "Percentage must be between 1 and 100")]
    [JsonPropertyName("beneficiary_percentage")]
    public int BeneficiaryPercentage { get; set; }
}