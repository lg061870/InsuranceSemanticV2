using ConversaCore.TopicFlow;
using InsuranceAgent.Models;
using InsuranceAgent.Topics;

namespace InsuranceAgent.Builders;

/// <summary>
/// Builds a complete LeadSummaryModel from the current workflow context,
/// aggregating all AdaptiveCard model data collected during the qualification flow.
/// </summary>
public static class LeadSummaryBuilder {
    public static LeadSummaryModel FromContext(TopicWorkflowContext context) {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var summary = new LeadSummaryModel();

        // --- LEAD DETAILS ---
        var leadDetails = context.GetValue<LeadDetailsModel>("LeadDetailsModel");
        summary.LeadDetails = leadDetails != null && !IsEmptyObject(leadDetails)
            ? leadDetails
            : null;

        // --- LIFE GOALS ---
        var lifeGoals = context.GetValue<LifeGoalsModel>("LifeGoalsModel");
        summary.LifeGoals = lifeGoals != null && !IsEmptyObject(lifeGoals)
            ? lifeGoals
            : null;

        // --- HEALTH INFO ---
        var healthInfo = context.GetValue<HealthInfoModel>("HealthInfoModel");
        summary.HealthInfo = healthInfo != null && !IsEmptyObject(healthInfo)
            ? healthInfo
            : null;

        // --- COVERAGE INTENT ---
        var coverageIntent = context.GetValue<CoverageIntentModel>("CoverageIntentModel");
        summary.CoverageIntent = coverageIntent != null && !IsEmptyObject(coverageIntent)
            ? coverageIntent
            : null;

        // --- DEPENDENTS ---
        var dependents = context.GetValue<DependentsModel>("DependentsModel");
        summary.Dependents = dependents != null && !IsEmptyObject(dependents)
            ? dependents
            : null;

        // --- EMPLOYMENT ---
        var employment = context.GetValue<EmploymentModel>("EmploymentModel");
        summary.Employment = employment != null && !IsEmptyObject(employment)
            ? employment
            : null;

        // --- BENEFICIARY INFO ---
        var beneficiaryInfo = context.GetValue<BeneficiaryInfoModel>("BeneficiaryInfoModel");
        summary.BeneficiaryInfo = beneficiaryInfo != null && !IsEmptyObject(beneficiaryInfo)
            ? beneficiaryInfo
            : null;

        // --- CONTACT INFO ---
        var contactInfo = context.GetValue<ContactInfoModel>("ContactInfoModel");
        summary.ContactInfo = contactInfo != null && !IsEmptyObject(contactInfo)
            ? contactInfo
            : null;

        // --- TOP-LEVEL FIELDS ---
        // Even if sections are null, we can still propagate top-level identifiers for reasoning
        var leadName = context.GetValue<string>("lead_name");
        var email = context.GetValue<string>("email");
        var phone = context.GetValue<string>("phone_number");

        if (leadName != null) {
            summary.LeadDetails ??= new LeadDetailsModel();
            //summary.LeadDetails.LeadName = leadName;
        }

        if (email != null || phone != null) {
            summary.ContactInfo ??= new ContactInfoModel();
            if (email != null) summary.ContactInfo.EmailAddress = email;
            if (phone != null) summary.ContactInfo.PhoneNumber = phone;
        }

        // --- Persist for later reasoning stages ---
        context.SetValue("lead_summary", summary);

        return summary;
    }


    /// <summary>
    /// Returns true if every writable property is null, empty string, false, or default.
    /// Used to suppress "empty" models.
    /// </summary>
    private static bool IsEmptyObject(object obj) {
        if (obj == null) return true;

        var props = obj.GetType().GetProperties()
            .Where(p => p.CanRead)
            .ToArray();

        foreach (var p in props) {
            var val = p.GetValue(obj);
            if (val == null) continue;

            if (val is string s && string.IsNullOrWhiteSpace(s)) continue;

            if (val is bool b && b == false) continue;

            if (val is int i && i == 0) continue;

            if (val is IEnumerable<object> e && !e.Any()) continue;

            // any meaningful value found
            return false;
        }

        return true;
    }

}

