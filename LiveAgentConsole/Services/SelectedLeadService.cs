using LiveAgentConsole.ViewModels;

namespace LiveAgentConsole.Services;

/// <summary>
/// Service to manage the currently selected lead for calling.
/// Provides reactive state management for lead selection across components.
/// </summary>
public class SelectedLeadService
{
    private LeadRowView? _selectedLead;

    /// <summary>
    /// Event that fires when a lead is selected or deselected
    /// </summary>
    public event Action? OnLeadSelectionChanged;

    /// <summary>
    /// Gets the currently selected lead
    /// </summary>
    public LeadRowView? SelectedLead => _selectedLead;

    /// <summary>
    /// Selects a lead to prepare for calling
    /// </summary>
    /// <param name="lead">The lead to select</param>
    public void SelectLead(LeadRowView lead)
    {
        _selectedLead = lead;
        OnLeadSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Clears the currently selected lead
    /// </summary>
    public void ClearSelection()
    {
        _selectedLead = null;
        OnLeadSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Checks if a specific lead is currently selected
    /// </summary>
    /// <param name="leadId">The lead ID to check</param>
    /// <returns>True if the lead is selected</returns>
    public bool IsLeadSelected(int leadId)
    {
        return _selectedLead?.Id == leadId;
    }
}
