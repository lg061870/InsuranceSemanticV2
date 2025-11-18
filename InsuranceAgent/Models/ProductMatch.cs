// Models/ProductMatch.cs
namespace InsuranceAgent.Models; 
public record ProductMatch(
    string Name,
    string Description,
    int MatchScore,
    int MonthlyPremium,
    string Coverage
);