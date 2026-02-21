# Service Testing Guide

## Prerequisites
1. Start the API: `dotnet run --project InsuranceSemanticV2.Api`
2. Start the Blazor app: `dotnet run --project LiveAgentConsoleV2`
3. Open browser to: `https://localhost:7058` (or your configured port)

---

## 1. Testing SignalR Hub (LeadsHub)

### Browser Console Testing
1. Open DevTools (F12) ? Console tab
2. Navigate to Home page (Dashboard)
3. Run these commands in console:

```javascript
// Check if SignalR is connected
console.log("SignalR State:", window.signalRConnection?.state);

// Monitor all SignalR events
window.signalRConnection?.on("LeadCreated", (leadId) => {
    console.log("?? LeadCreated event:", leadId);
});

window.signalRConnection?.on("LeadUpdated", (leadId) => {
    console.log("?? LeadUpdated event:", leadId);
});

window.signalRConnection?.on("ProfileUpdated", (leadId) => {
    console.log("?? ProfileUpdated event:", leadId);
});

window.signalRConnection?.on("KpisChanged", () => {
    console.log("?? KpisChanged event triggered");
});
```

### Test Scenarios:
1. **Create a lead via API** (use Swagger or Postman):
   - POST `/api/leads` with a test lead
   - Watch console for `LeadCreated` and `KpisChanged` events
   - Dashboard should auto-refresh

2. **Update a lead**:
   - PUT `/api/leads/{id}` 
   - Watch for `LeadUpdated` event

3. **Update profile**:
   - PUT `/api/leads/{id}/profile`
   - Watch for `ProfileUpdated` and `KpisChanged` events

---

## 2. Testing KpiService

### Manual API Testing (Swagger)
1. Navigate to: `http://localhost:5031/swagger`
2. Find **GET /api/leads/kpis** endpoint
3. Click "Try it out" ? "Execute"
4. Verify response includes:
   ```json
   {
     "totalLeads": 5,
     "hotLeads": 2,
     "warmLeads": 2,
     "coldLeads": 1,
     "conversionRate": 15.5,
     "averageResponseTime": "00:05:23"
   }
   ```

### Browser Network Tab
1. Open DevTools ? Network tab
2. Filter by "kpis"
3. Refresh dashboard
4. Check HTTP request to `/api/leads/kpis`
5. Verify 200 OK response with JSON payload

### PowerShell Testing
```powershell
# Test KPI endpoint directly
Invoke-RestMethod -Uri "http://localhost:5031/api/leads/kpis" -Method GET | ConvertTo-Json

# With formatted output
$kpis = Invoke-RestMethod -Uri "http://localhost:5031/api/leads/kpis"
Write-Host "Total Leads: $($kpis.totalLeads)"
Write-Host "Hot: $($kpis.hotLeads) | Warm: $($kpis.warmLeads) | Cold: $($kpis.coldLeads)"
```

---

## 3. Testing SessionService

### Current Implementation (Logging Only)
The SessionService currently logs to console. To test:

1. **Check Console Logs** when Home.razor loads:
   ```
   Starting session with connectionId: ABC123...
   ```

2. **Monitor Heartbeats** (every 30 seconds):
   ```
   Heartbeat sent
   ```

3. **Check Session End** when navigating away:
   ```
   Session ended
   ```

### Future API Integration Testing
Once SessionService is connected to API endpoints:

```powershell
# Start session
$sessionResponse = Invoke-RestMethod -Uri "http://localhost:5031/api/sessions/start" -Method POST -Body (@{
    agentId = 1
    connectionId = "test-connection-123"
} | ConvertTo-Json) -ContentType "application/json"

$sessionId = $sessionResponse.sessionId

# Send heartbeat
Invoke-RestMethod -Uri "http://localhost:5031/api/sessions/$sessionId/heartbeat" -Method POST

# End session
Invoke-RestMethod -Uri "http://localhost:5031/api/sessions/$sessionId/end" -Method POST
```

---

## 4. Testing LeadHubConnection (Client Service)

### Blazor Component Test
Add this test component to verify events:

**File:** `LiveAgentConsoleV2/Pages/TestHub.razor`

```razor
@page "/test-hub"
@inject LeadHubConnection HubConnection
@implements IAsyncDisposable

<h3>SignalR Hub Tester</h3>

<div>
    <p>Connection State: <strong>@connectionState</strong></p>
    <p>Connection ID: <strong>@connectionId</strong></p>
</div>

<h4>Events Received:</h4>
<ul>
    @foreach (var evt in events)
    {
        <li>@evt</li>
    }
</ul>

<button @onclick="TestConnection">Reconnect</button>
<button @onclick="ClearEvents">Clear Events</button>

@code {
    private string connectionState = "Unknown";
    private string connectionId = "Not connected";
    private List<string> events = new();

    protected override async Task OnInitializedAsync()
    {
        HubConnection.OnLeadCreated += HandleLeadCreated;
        HubConnection.OnLeadUpdated += HandleLeadUpdated;
        HubConnection.OnProfileUpdated += HandleProfileUpdated;
        HubConnection.OnKpisChanged += HandleKpisChanged;

        await HubConnection.StartAsync();
        UpdateConnectionInfo();
    }

    private async Task HandleLeadCreated(int leadId)
    {
        await InvokeAsync(() =>
        {
            events.Insert(0, $"[{DateTime.Now:HH:mm:ss}] LeadCreated: {leadId}");
            StateHasChanged();
        });
    }

    private async Task HandleLeadUpdated(int leadId)
    {
        await InvokeAsync(() =>
        {
            events.Insert(0, $"[{DateTime.Now:HH:mm:ss}] LeadUpdated: {leadId}");
            StateHasChanged();
        });
    }

    private async Task HandleProfileUpdated(int leadId)
    {
        await InvokeAsync(() =>
        {
            events.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ProfileUpdated: {leadId}");
            StateHasChanged();
        });
    }

    private async Task HandleKpisChanged()
    {
        await InvokeAsync(() =>
        {
            events.Insert(0, $"[{DateTime.Now:HH:mm:ss}] KpisChanged");
            StateHasChanged();
        });
    }

    private async Task TestConnection()
    {
        await HubConnection.StopAsync();
        await HubConnection.StartAsync();
        UpdateConnectionInfo();
    }

    private void ClearEvents()
    {
        events.Clear();
    }

    private void UpdateConnectionInfo()
    {
        connectionState = HubConnection.State.ToString();
        connectionId = HubConnection.ConnectionId ?? "Not connected";
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        HubConnection.OnLeadCreated -= HandleLeadCreated;
        HubConnection.OnLeadUpdated -= HandleLeadUpdated;
        HubConnection.OnProfileUpdated -= HandleProfileUpdated;
        HubConnection.OnKpisChanged -= HandleKpisChanged;
        await HubConnection.StopAsync();
    }
}
```

**Usage:**
1. Navigate to `/test-hub` in browser
2. Open Swagger in another tab
3. Create/update leads via API
4. Watch events appear in real-time on the test page

---

## 5. Running Automated Tests

### Run All Integration Tests
```powershell
# From solution root
dotnet test InsuranceSemanticV2.IntegrationTests

# Run specific test class
dotnet test --filter "FullyQualifiedName~LeadsHubTests"

# Verbose output
dotnet test InsuranceSemanticV2.IntegrationTests --verbosity detailed
```

### Run Individual Tests
```powershell
# Run SignalR connection test
dotnet test --filter "FullyQualifiedName~SignalRHub_ShouldConnect_Successfully"

# Run KPI test
dotnet test --filter "FullyQualifiedName~GetKpis_ShouldReturnCorrectCounts"
```

---

## 6. Troubleshooting

### SignalR Not Connecting
- Check API is running on correct port (5031)
- Verify CORS configuration in `Program.cs`
- Check browser console for errors
- Ensure `UsePathBase` isn't affecting `/hubs` route

### KPI Service Returns Null
- Check HttpClient base address in DI registration
- Verify API endpoint changed from `/leads/kpis` to `/api/leads/kpis`
- Check for network errors in browser DevTools

### Session Service Not Working
- Check console logs for connection ID
- Verify heartbeat timer is running (30-second intervals)
- SessionService currently only logs - API integration pending

---

## 7. Performance Testing

### Load Test SignalR Hub
```csharp
// Create 100 concurrent connections
var connections = new List<HubConnection>();
for (int i = 0; i < 100; i++)
{
    var hub = new HubConnectionBuilder()
        .WithUrl("http://localhost:5031/hubs/leads")
        .Build();
    await hub.StartAsync();
    connections.Add(hub);
}

// Trigger events and measure broadcast time
var sw = Stopwatch.StartNew();
// Create lead via API...
await Task.Delay(1000);
sw.Stop();
Console.WriteLine($"Broadcast to {connections.Count} clients: {sw.ElapsedMilliseconds}ms");
```

---

## Next Steps
1. ? Run integration tests: `dotnet test`
2. ? Create `/test-hub` page for manual testing
3. ? Implement SessionService API integration
4. ? Add SignalR authentication with JWT
5. ? Add reconnection logging in `LeadHubConnection`
