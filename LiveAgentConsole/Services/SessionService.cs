using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LiveAgentConsole.Services;

public class SessionService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private readonly LeadHubConnection _hubConnection;
    private Timer? _heartbeatTimer;
    private int? _sessionId;
    private bool _isDisposed;

    public SessionService(
        HttpClient httpClient,
        AuthService authService,
        LeadHubConnection hubConnection)
    {
        _httpClient = httpClient;
        _authService = authService;
        _hubConnection = hubConnection;
    }

    public async Task StartSessionAsync(string connectionId)
    {
        try
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                ConnectionId = connectionId,
                IpAddress = (string?)null,  // Let server extract from HttpContext
                UserAgent = (string?)null   // Let server extract from HttpContext
            };

            var response = await _httpClient.PostAsJsonAsync("api/sessions/start", request);
            if (response.IsSuccessStatusCode)
            {
                var sessionResponse = await response.Content.ReadFromJsonAsync<SessionStartResponse>();
                if (sessionResponse != null)
                {
                    _sessionId = sessionResponse.SessionId;

                    // Start heartbeat timer (30 seconds interval)
                    _heartbeatTimer = new Timer(
                        SendHeartbeat,
                        null,
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromSeconds(30));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start session: {ex.Message}");
        }
    }

    public async Task EndSessionAsync()
    {
        // Stop heartbeat timer
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
            _heartbeatTimer = null;
        }

        try
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            await _httpClient.PostAsync("api/sessions/end", null);
            _sessionId = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to end session: {ex.Message}");
        }
    }

    private async void SendHeartbeat(object? state)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                // Token missing, stop heartbeat
                if (_heartbeatTimer != null)
                {
                    await _heartbeatTimer.DisposeAsync();
                    _heartbeatTimer = null;
                }
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            await _httpClient.PostAsync("api/sessions/heartbeat", null);
        }
        catch (Exception ex)
        {
            // Log but don't crash - network issues are expected
            Console.WriteLine($"Heartbeat failed: {ex.Message}");
        }
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
