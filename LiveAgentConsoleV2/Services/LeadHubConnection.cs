using Microsoft.AspNetCore.SignalR.Client;

namespace LiveAgentConsoleV2.Services;

public class LeadHubConnection : IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly string _hubUrl;

    public event Func<int, Task>? OnLeadCreated;
    public event Func<int, Task>? OnLeadUpdated;
    public event Func<int, Task>? OnProfileUpdated;
    public event Func<Task>? OnKpisChanged;
    public event Func<int, string, Task>? OnAgentConnected;
    public event Func<int, Task>? OnAgentDisconnected;
    public event Func<int, string, Task>? OnAgentStatusChanged;

    public LeadHubConnection(string hubUrl)
    {
        try
        {
            Console.WriteLine($"[LeadHubConnection] Constructing with URL: {hubUrl}");
            _hubUrl = hubUrl;

            Console.WriteLine("[LeadHubConnection] Building HubConnection...");
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            Console.WriteLine("[LeadHubConnection] Registering event handlers...");
            _hubConnection.On<int>("LeadCreated", async (leadId) =>
            {
                if (OnLeadCreated != null)
                    await OnLeadCreated.Invoke(leadId);
            });

            _hubConnection.On<int>("LeadUpdated", async (leadId) =>
            {
                if (OnLeadUpdated != null)
                    await OnLeadUpdated.Invoke(leadId);
            });

            _hubConnection.On<int>("ProfileUpdated", async (leadId) =>
            {
                if (OnProfileUpdated != null)
                    await OnProfileUpdated.Invoke(leadId);
            });

            _hubConnection.On("KpisChanged", async () =>
            {
                if (OnKpisChanged != null)
                    await OnKpisChanged.Invoke();
            });

            _hubConnection.On<int, string>("AgentConnected", async (agentId, agentName) =>
            {
                if (OnAgentConnected != null)
                    await OnAgentConnected.Invoke(agentId, agentName);
            });

            _hubConnection.On<int>("AgentDisconnected", async (agentId) =>
            {
                if (OnAgentDisconnected != null)
                    await OnAgentDisconnected.Invoke(agentId);
            });

            _hubConnection.On<int, string>("AgentStatusChanged", async (agentId, status) =>
            {
                if (OnAgentStatusChanged != null)
                    await OnAgentStatusChanged.Invoke(agentId, status);
            });

            Console.WriteLine("[LeadHubConnection] Constructor completed successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LeadHubConnection] ERROR in constructor: {ex}");
            throw;
        }
    }

    public async Task StartAsync()
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            await _hubConnection.StartAsync();
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.StopAsync();
        }
    }

    public HubConnectionState State => _hubConnection.State;

    public string? ConnectionId => _hubConnection.ConnectionId;

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }
}
