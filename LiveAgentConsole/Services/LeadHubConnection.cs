using Microsoft.AspNetCore.SignalR.Client;

namespace LiveAgentConsole.Services;

public class LeadHubConnection : IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly AuthService _authService;
    private readonly string _hubUrl;

    public event Func<int, Task>? OnLeadCreated;
    public event Func<int, Task>? OnLeadUpdated;
    public event Func<int, Task>? OnProfileUpdated;
    public event Func<Task>? OnKpisChanged;
    public event Func<int, string, Task>? OnAgentConnected;
    public event Func<int, Task>? OnAgentDisconnected;
    public event Func<int, string, Task>? OnAgentStatusChanged;

    public LeadHubConnection(string hubUrl, AuthService authService)
    {
        _hubUrl = hubUrl;
        _authService = authService;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Token will be set in StartAsync() since constructor runs before auth is available
                options.AccessTokenProvider = async () => await _authService.GetTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

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
