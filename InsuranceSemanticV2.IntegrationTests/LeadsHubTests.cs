using Microsoft.AspNetCore.SignalR.Client;
using InsuranceSemanticV2.Core.DTO;
using System.Net.Http.Json;

namespace InsuranceSemanticV2.IntegrationTests;

public class LeadsHubTests : IntegrationTestBase
{
    [Fact]
    public async Task SignalRHub_ShouldConnect_Successfully()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{Client.BaseAddress}hubs/leads", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        // Act
        await hubConnection.StartAsync();

        // Assert
        Assert.Equal(HubConnectionState.Connected, hubConnection.State);

        // Cleanup
        await hubConnection.StopAsync();
        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task LeadCreated_ShouldTriggerSignalREvent()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{Client.BaseAddress}hubs/leads", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        var leadCreatedReceived = false;
        var receivedLeadId = 0;

        hubConnection.On<int>("LeadCreated", (leadId) =>
        {
            leadCreatedReceived = true;
            receivedLeadId = leadId;
        });

        await hubConnection.StartAsync();

        // Act - Create a lead via API
        var newLead = new LeadRequest
        {
            FullName = "SignalR Test",
            Email = "signalr@test.com",
            Phone = "555-9999",
            Status = "New",
            LeadSource = "Test"
        };

        var response = await Client.PostAsJsonAsync("/api/leads", newLead);
        Assert.True(response.IsSuccessStatusCode);

        // Wait for SignalR event
        await Task.Delay(500);

        // Assert
        Assert.True(leadCreatedReceived, "LeadCreated event was not received");
        Assert.True(receivedLeadId > 0, "Received lead ID should be greater than 0");

        // Cleanup
        await hubConnection.StopAsync();
        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task KpisChanged_ShouldTriggerSignalREvent()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{Client.BaseAddress}hubs/leads", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        var kpisChangedReceived = false;

        hubConnection.On("KpisChanged", () =>
        {
            kpisChangedReceived = true;
        });

        await hubConnection.StartAsync();

        // Act - Create a lead (should trigger KpisChanged)
        var newLead = new LeadRequest
        {
            FullName = "KPI Test",
            Email = "kpi@test.com",
            Phone = "555-8888",
            Status = "New",
            LeadSource = "Test"
        };

        await Client.PostAsJsonAsync("/api/leads", newLead);

        // Wait for SignalR event
        await Task.Delay(500);

        // Assert
        Assert.True(kpisChangedReceived, "KpisChanged event was not received");

        // Cleanup
        await hubConnection.StopAsync();
        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task ProfileUpdated_ShouldTriggerSignalREvent()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"{Client.BaseAddress}hubs/leads", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        var profileUpdatedReceived = false;
        var receivedLeadId = 0;

        hubConnection.On<int>("ProfileUpdated", (leadId) =>
        {
            profileUpdatedReceived = true;
            receivedLeadId = leadId;
        });

        await hubConnection.StartAsync();

        // Create a lead first
        var newLead = new LeadRequest
        {
            FullName = "Profile Test",
            Email = "profile@test.com",
            Phone = "555-7777",
            Status = "New",
            LeadSource = "Test"
        };

        var createResponse = await Client.PostAsJsonAsync("/api/leads", newLead);
        var createdLead = await createResponse.Content.ReadFromJsonAsync<LeadResponse>();
        var leadId = createdLead!.LeadId;

        // Act - Update profile (mark progress flags)
        var profileUpdate = new LeadRequest
        {
            LeadId = leadId,
            FullName = newLead.FullName,
            Email = newLead.Email,
            Phone = newLead.Phone,
            Status = "Contacted",
            HasContactInfo = true
        };

        await Client.PutAsJsonAsync($"/api/leads/{leadId}/profile", profileUpdate);

        // Wait for SignalR event
        await Task.Delay(500);

        // Assert
        Assert.True(profileUpdatedReceived, "ProfileUpdated event was not received");
        Assert.Equal(leadId, receivedLeadId);

        // Cleanup
        await hubConnection.StopAsync();
        await hubConnection.DisposeAsync();
    }
}
