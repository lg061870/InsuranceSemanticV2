# FEATURE: Agent Authentication & Session Tracking

**Status:** ✅ COMPLETE - All 5 Phases Implemented
**Priority:** High (Foundation for real-time features)
**Target Application:** LiveAgentConsole
**Progress:** 5 of 5 phases complete (100%)

## Overview
Implement a lightweight agent authentication system (demo-level, not production-grade) that allows agents to log in, establishes their session, and tracks their online status in real-time. This is required for multi-agent coordination, click-to-call functionality, and activity monitoring.

## Goals
1. Allow agents to log in with basic credentials (no complex auth flow)
2. Create and track agent sessions in the database
3. Maintain real-time agent status (Online, Offline, Away, On Call)
4. Associate agent actions (lead updates, calls) with the authenticated agent
5. Enable agent presence awareness across the system

## User Stories

### Agent Login
**As an agent**, I want to log in to the LiveAgent console so that my actions are tracked and associated with my identity.

**Acceptance Criteria:**
- Agent sees a login screen when accessing LiveAgentConsole
- Agent can enter username/email and password
- Upon successful login, agent is redirected to dashboard
- Agent's session is created in the database
- Agent's SignalR connection is associated with their session

### Session Tracking
**As the system**, I need to track active agent sessions so that I can route calls, monitor availability, and coordinate multi-agent workflows.

**Acceptance Criteria:**
- Each agent login creates a new AgentSession record
- Session includes: AgentId, ConnectionId (SignalR), LoginTime, LastActivityTime, Status
- Session is updated on agent activity (heartbeat)
- Session is ended when agent logs out or disconnects
- Stale sessions are cleaned up automatically

### Agent Presence
**As an agent supervisor**, I want to see which agents are currently online so that I can understand team capacity.

**Acceptance Criteria:**
- Dashboard shows count of active agents
- Agent status is visible: Online, Offline, Away, On Call
- Status updates in real-time via SignalR

## Technical Design

### Database Schema

#### New Table: `AgentSessions`
```sql
CREATE TABLE AgentSessions (
    AgentSessionId INT PRIMARY KEY IDENTITY(1,1),
    AgentId INT NOT NULL,
    ConnectionId NVARCHAR(100) NOT NULL,        -- SignalR connection ID
    LoginTime DATETIME2 NOT NULL,
    LastActivityTime DATETIME2 NOT NULL,
    LogoutTime DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL,               -- Online, Away, OnCall, Offline
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_AgentSessions_Agent FOREIGN KEY (AgentId) REFERENCES Agents(AgentId)
);

CREATE INDEX IX_AgentSessions_AgentId ON AgentSessions(AgentId);
CREATE INDEX IX_AgentSessions_ConnectionId ON AgentSessions(ConnectionId);
CREATE INDEX IX_AgentSessions_IsActive ON AgentSessions(IsActive);
```

#### Update Existing Tables
Add `AgentId` to tables that need agent tracking:
- `Leads` - add `LastModifiedByAgentId` (INT, nullable, FK to Agents)
- `ProfileProgress` - already has relationship through Lead
- Consider: Call logs, notes, activity logs

### Authentication Flow (Demo-Level)

**Login Process:**
1. Agent navigates to `/login`
2. Enters credentials (username/password)
3. Backend validates against `Agents` table
4. On success:
   - Create JWT token (simple, claims: AgentId, AgentName, Role)
   - Store token in browser localStorage
   - Create `AgentSession` record
   - Redirect to `/`

5. On dashboard load:
   - Validate JWT token
   - Establish SignalR connection
   - Update AgentSession with ConnectionId
   - Set status to "Online"

**Logout Process:**
1. Agent clicks logout
2. Update AgentSession: set `LogoutTime`, `IsActive = false`
3. Disconnect SignalR
4. Clear localStorage token
5. Redirect to `/login`

**Session Heartbeat:**
- Every 30 seconds, update `LastActivityTime`
- If no heartbeat for 5 minutes, mark session as "Away"
- If no heartbeat for 15 minutes, mark session as "Offline" and end session

### Components to Create

#### 1. Login Page (`LiveAgentConsole/Pages/Login.razor`)
```razor
@page "/login"
@layout EmptyLayout

<div class="min-h-screen flex items-center justify-center bg-gray-50">
    <div class="max-w-md w-full bg-white p-8 rounded-lg shadow-md">
        <h2>Agent Login</h2>
        <form @onsubmit="HandleLogin">
            <input @bind="username" placeholder="Username or Email" />
            <input @bind="password" type="password" placeholder="Password" />
            <button type="submit">Login</button>
        </form>
    </div>
</div>
```

#### 2. Auth Service (`LiveAgentConsole/Services/AuthService.cs`)
```csharp
public class AuthService
{
    public Task<LoginResult> LoginAsync(string username, string password);
    public Task LogoutAsync();
    public Task<Agent?> GetCurrentAgentAsync();
    public Task UpdateActivityAsync();
    public Task SetStatusAsync(AgentStatus status);
}
```

#### 3. Auth State Provider (Blazor Authentication State)
```csharp
public class AgentAuthenticationStateProvider : AuthenticationStateProvider
{
    // Manage authentication state
    // Read/validate JWT from localStorage
    // Provide ClaimsPrincipal with AgentId
}
```

#### 4. Session Service (`LiveAgentConsole/Services/SessionService.cs`)
```csharp
public class SessionService
{
    private System.Threading.Timer? _heartbeatTimer;

    public Task StartSessionAsync(int agentId, string connectionId);
    public Task EndSessionAsync();
    public Task UpdateLastActivityAsync();
    public Task<List<AgentSession>> GetActiveSessionsAsync();
}
```

### API Endpoints

#### Authentication Endpoints
```csharp
// InsuranceSemanticV2.Api/Endpoints/AuthEndpoints.cs

POST /api/auth/login
{
    "username": "agent@example.com",
    "password": "demo123"
}
Response: { "token": "jwt...", "agent": { ... } }

POST /api/auth/logout
Authorization: Bearer {token}
Response: { "success": true }

GET /api/auth/me
Authorization: Bearer {token}
Response: { "agentId": 1, "name": "John Doe", "status": "Online" }

PUT /api/auth/status
Authorization: Bearer {token}
Body: { "status": "Away" }
Response: { "success": true }
```

#### Session Endpoints
```csharp
// InsuranceSemanticV2.Api/Endpoints/SessionEndpoints.cs

POST /api/sessions/start
Authorization: Bearer {token}
Body: { "connectionId": "abc123", "ipAddress": "...", "userAgent": "..." }
Response: { "sessionId": 42 }

POST /api/sessions/heartbeat
Authorization: Bearer {token}
Response: { "success": true }

POST /api/sessions/end
Authorization: Bearer {token}
Response: { "success": true }

GET /api/sessions/active
Authorization: Bearer {token}
Response: [{ "agentId": 1, "agentName": "...", "status": "Online", ... }]
```

### SignalR Integration

#### Hub Changes
```csharp
// InsuranceSemanticV2.Api/Hubs/LeadsHub.cs

public class LeadsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Associate connection with agent session
        var agentId = GetAgentIdFromContext();
        await UpdateAgentConnection(agentId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        // End agent session
        var agentId = GetAgentIdFromContext();
        await EndAgentSession(agentId, Context.ConnectionId);
        await base.OnDisconnectedAsync(ex);
    }
}
```

#### New SignalR Events
- `AgentStatusChanged(int agentId, string status)` - Broadcast when agent status changes
- `AgentConnected(int agentId, string agentName)` - Broadcast when agent logs in
- `AgentDisconnected(int agentId)` - Broadcast when agent logs out

### Security Considerations (Demo-Level)

**What We're Implementing:**
- Simple JWT tokens with AgentId claim
- Token stored in localStorage
- Bearer token authentication on API
- Password comparison (plain text or simple hash for demo)

**What We're NOT Implementing (Production Requirements):**
- ❌ Password hashing with salt (bcrypt/Argon2)
- ❌ HTTPS enforcement (assume local dev)
- ❌ Refresh tokens
- ❌ CSRF protection
- ❌ Rate limiting on login
- ❌ Account lockout after failed attempts
- ❌ Email verification
- ❌ Password reset flow
- ❌ Multi-factor authentication

**Important Note:** This is explicitly a **demo-level authentication system**. Before production deployment, a proper authentication system (e.g., ASP.NET Core Identity, Auth0, Azure AD) must be implemented.

### Configuration

#### appsettings.json
```json
{
  "Jwt": {
    "SecretKey": "demo-secret-key-min-32-chars-long-12345",
    "Issuer": "InsuranceSemanticV2.Api",
    "Audience": "LiveAgentConsole",
    "ExpirationMinutes": 480
  },
  "Session": {
    "HeartbeatIntervalSeconds": 30,
    "AwayTimeoutMinutes": 5,
    "OfflineTimeoutMinutes": 15
  }
}
```

### UI Components

#### Agent Indicator (Header)
Update Header to show logged-in agent:
```razor
<div class="flex items-center gap-3">
    <div class="text-right">
        <p class="text-sm font-bold">@CurrentAgent.Name</p>
        <p class="text-xs text-green-600">
            <span class="w-2 h-2 bg-green-500 rounded-full"></span> @CurrentAgent.Status
        </p>
    </div>
    <img src="@CurrentAgent.AvatarUrl" />
</div>
```

#### Active Agents Widget (Dashboard - Optional)
```razor
<div class="bg-white p-4 rounded-lg shadow">
    <h3>Team Status</h3>
    <div class="space-y-2">
        @foreach (var session in ActiveSessions)
        {
            <div class="flex items-center justify-between">
                <span>@session.AgentName</span>
                <span class="badge">@session.Status</span>
            </div>
        }
    </div>
</div>
```

## Data Migration

### Migration Steps
1. Create `AgentSessions` table
2. Add `LastModifiedByAgentId` to `Leads` table (nullable)
3. Seed demo agents if `Agents` table is empty

### Seed Data
```sql
-- Demo agents for testing
INSERT INTO Agents (FirstName, LastName, Email, Phone, HireDate, Status)
VALUES
    ('John', 'Doe', 'john.doe@example.com', '555-0101', GETDATE(), 'Active'),
    ('Jane', 'Smith', 'jane.smith@example.com', '555-0102', GETDATE(), 'Active'),
    ('Bob', 'Johnson', 'bob.johnson@example.com', '555-0103', GETDATE(), 'Active');
```

## Testing Plan

### Manual Testing
1. **Login Flow:**
   - Navigate to `/login`
   - Enter valid credentials → redirected to dashboard
   - Enter invalid credentials → error message shown
   - Token stored in localStorage

2. **Session Tracking:**
   - After login, verify `AgentSession` created in database
   - Verify `ConnectionId` matches SignalR connection
   - Check `LastActivityTime` updates periodically

3. **Logout Flow:**
   - Click logout button
   - Verify `AgentSession.IsActive` set to false
   - Verify redirected to `/login`
   - Verify token cleared from localStorage

4. **Multi-Tab Testing:**
   - Open LiveAgent in two browser tabs
   - Login in both → two sessions created
   - Close one tab → that session ends
   - Other tab continues working

5. **Reconnection:**
   - Login
   - Kill network connection
   - Restore connection
   - Verify session reconnects with new ConnectionId

### Integration Testing
```csharp
[Fact]
public async Task Login_WithValidCredentials_ReturnsToken()
{
    var response = await Client.PostAsJsonAsync("/api/auth/login", new {
        username = "john.doe@example.com",
        password = "demo123"
    });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
    result.Token.Should().NotBeNullOrEmpty();
}

[Fact]
public async Task StartSession_CreatesSessionInDatabase()
{
    // Arrange: Login to get token
    var token = await LoginAsAgent();

    // Act: Start session
    var response = await Client.PostAsJsonAsync("/api/sessions/start",
        new { connectionId = "test-123" },
        token);

    // Assert: Session exists in DB
    var session = await DbContext.AgentSessions
        .FirstOrDefaultAsync(s => s.ConnectionId == "test-123");
    session.Should().NotBeNull();
    session.IsActive.Should().BeTrue();
}
```

## Dependencies

