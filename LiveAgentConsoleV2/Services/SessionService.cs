namespace LiveAgentConsoleV2.Services;

public class SessionService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private Timer? _heartbeatTimer;
    private int? _sessionId;
    private bool _isDisposed;

    public SessionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task StartSessionAsync(string connectionId)
    {
        // TODO: Implement session start logic
        Console.WriteLine($"Starting session with connectionId: {connectionId}");
        
        // Start heartbeat timer (30 seconds interval)
        _heartbeatTimer = new Timer(
            SendHeartbeat,
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
        
        return Task.CompletedTask;
    }

    public async Task EndSessionAsync()
    {
        // Stop heartbeat timer
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
            _heartbeatTimer = null;
        }

        Console.WriteLine("Session ended");
        _sessionId = null;
    }

    private void SendHeartbeat(object? state)
    {
        if (_isDisposed)
        {
            return;
        }

        // TODO: Implement heartbeat logic
        Console.WriteLine("Heartbeat sent");
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        await EndSessionAsync();

        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
            _heartbeatTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}

public class SessionStartResponse
{
    public int SessionId { get; set; }
}
