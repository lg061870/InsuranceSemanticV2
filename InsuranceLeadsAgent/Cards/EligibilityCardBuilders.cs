using System.Text.Json;

namespace InsuranceLeadsAgent.Cards;

/// <summary>
/// Builder type for the combined pre-qualification adaptive card used in
/// the eligibility flow. This wraps the static JSON template in
/// LifeInsuranceCards so it can be used with AdaptiveCardActivity&lt;TCard, TModel&gt;.
/// </summary>
public class PreQualificationCard {
    public object Create() => JsonSerializer.Deserialize<object>(LifeInsuranceCards.PreQualificationCard())!;
}
