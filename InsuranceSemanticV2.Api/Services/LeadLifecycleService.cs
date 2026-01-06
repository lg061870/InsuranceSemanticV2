using InsuranceSemanticV2.Data.Entities;
using Microsoft.Extensions.Options;

namespace InsuranceSemanticV2.Api.Services;

public class LeadLifecycleOptions
{
    public int OnHoldThresholdMinutes { get; set; } = 5;
    public int AbandonedThresholdHours { get; set; } = 10;
}

public class LeadLifecycleService
{
    private readonly LeadLifecycleOptions _options;

    public LeadLifecycleService(IOptions<LeadLifecycleOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Updates the lead status based on UpdatedAt timestamp.
    /// Returns true if status was changed, false if no change.
    /// IMPORTANT: Skips leads already in "Abandoned" status.
    /// </summary>
    public bool UpdateLeadStatus(Lead lead)
    {
        // Critical: Never update abandoned leads
        if (lead.Status?.Equals("Abandoned", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var timeSinceUpdate = now - lead.UpdatedAt;
        var originalStatus = lead.Status;

        // State transition logic
        if (timeSinceUpdate.TotalHours >= _options.AbandonedThresholdHours)
        {
            // Check if lead has contact information
            var hasContactInfo = !string.IsNullOrWhiteSpace(lead.Email) ||
                                !string.IsNullOrWhiteSpace(lead.Phone);

            lead.Status = hasContactInfo ? "To-Rescue" : "Abandoned";
        }
        else if (timeSinceUpdate.TotalMinutes >= _options.OnHoldThresholdMinutes)
        {
            // Only transition to On-Hold if not already in a later state
            if (!IsLaterState(lead.Status))
            {
                lead.Status = "On-Hold";
            }
        }
        // else: keep current status (Active/Live)

        return lead.Status != originalStatus;
    }

    /// <summary>
    /// Determines if the current status is a "later" state in the lifecycle
    /// to avoid regressing states.
    /// </summary>
    private bool IsLaterState(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;

        return status.Equals("To-Rescue", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Abandoned", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the display color for a lifecycle state.
    /// </summary>
    public static string GetStatusColor(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "active" or "live" or "new" => "text-green-600",
            "on-hold" => "text-yellow-600",
            "to-rescue" => "text-orange-600",
            "abandoned" => "text-gray-400",
            _ => "text-blue-600"
        };
    }

    /// <summary>
    /// Gets the background color for status badges.
    /// </summary>
    public static string GetStatusBgColor(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "active" or "live" or "new" => "bg-green-100",
            "on-hold" => "bg-yellow-100",
            "to-rescue" => "bg-orange-100",
            "abandoned" => "bg-gray-100",
            _ => "bg-blue-100"
        };
    }
}
