namespace InsuranceSemanticV2.Api.Endpoints;

public static class EndpointExtensions {
    public static void MapApiEndpoints(this WebApplication app) {
        app.MapLeadEndpoints();
        app.MapProfileEndpoints();
        app.MapAgentEndpoints();
        app.MapStatusEndpoints();
        app.MapInteractionEndpoints();
        app.MapScoreEndpoints();
        app.MapSchedulingEndpoints();
        app.MapComplianceEndpoints();
        app.MapCarrierEndpoints();
        app.MapAuthEndpoints();
        app.MapSessionEndpoints();
        app.MapCarrierStateComplianceEndpoints();
        app.MapAgentAvailabilityEndpoints();
        app.MapContactPolicyEndpoints();
    }
}
