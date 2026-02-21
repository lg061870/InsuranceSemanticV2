namespace InsuranceSemanticV2.Api.Endpoints;

public static class EndpointExtensions {
    public static void MapApiEndpoints(this WebApplication app) {
        var apiGroup = app.MapGroup("/api");
        
        apiGroup.MapLeadEndpoints();
        apiGroup.MapProfileEndpoints();
        apiGroup.MapAgentEndpoints();
        apiGroup.MapStatusEndpoints();
        apiGroup.MapInteractionEndpoints();
        apiGroup.MapScoreEndpoints();
        apiGroup.MapSchedulingEndpoints();
        apiGroup.MapComplianceEndpoints();
        apiGroup.MapCarrierEndpoints();
        apiGroup.MapAuthEndpoints();
        apiGroup.MapSessionEndpoints();
        apiGroup.MapCarrierStateComplianceEndpoints();
        apiGroup.MapAgentAvailabilityEndpoints();
        apiGroup.MapContactPolicyEndpoints();
    }
}
