# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution Structure

This is a hybrid solution containing two distinct application layers:

### 1. ConversaCore/InsuranceAgent (Blazor Server)
Legacy conversational AI framework with topic-based conversation flows. Primary UI is a Blazor Server application.

### 2. InsuranceSemanticV2 (REST API + Data Layer)
Modern REST API with EF Core data layer, minimal API endpoints, and integration testing.

**Projects:**
- `ConversaCore/`: Core conversation framework library
- `InsuranceAgent/`: Blazor Server UI application
- `ConversaCore.Tests/`: xUnit tests for ConversaCore
- `InsuranceSemanticV2.Core/`: DTOs and shared models
- `InsuranceSemanticV2.Data/`: EF Core entities, DbContext, migrations
- `InsuranceSemanticV2.Api/`: Minimal API with endpoints + SignalR hubs
- `InsuranceSemanticV2.IntegrationTests/`: API integration tests
- `LiveAgentConsole/`: Live agent interface (Blazor WebAssembly with SignalR)

## Development Commands

### Building
```bash
dotnet build                              # Build entire solution
dotnet build InsuranceAgent               # Build specific project
```

### Running Applications
```bash
# Blazor Server (ConversaCore/InsuranceAgent)
cd InsuranceAgent
dotnet run

# REST API (InsuranceSemanticV2.Api)
cd InsuranceSemanticV2.Api
dotnet run
```

### Database Operations (EF Core)
```bash
# Add migration
dotnet ef migrations add MigrationName --project InsuranceSemanticV2.Data --startup-project InsuranceSemanticV2.Api

# Update database
dotnet ef database update --project InsuranceSemanticV2.Data --startup-project InsuranceSemanticV2.Api

# List migrations
dotnet ef migrations list --project InsuranceSemanticV2.Data --startup-project InsuranceSemanticV2.Api
```

### Testing
```bash
# Run all tests
dotnet test

# Run ConversaCore tests with diagnostics
./tests.ps1

# Run integration tests only
dotnet test InsuranceSemanticV2.IntegrationTests

# Run specific test class
dotnet test --filter "FullyQualifiedName~LeadsEndpointsTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Architecture

### ConversaCore Framework (Topic-Based Conversations)

**Topic Flow Pattern:**
- Topics inherit from `TopicFlow` and contain a FIFO queue of activities
- Activities execute sequentially (queued via `Add()` method)
- Each topic has an explicit state machine using `TopicStateMachine<TState>`

**Hand-Down/Regain Control:**
Topics can call sub-topics and wait for completion:
```csharp
Add(new TriggerTopicActivity(
    "call-subtopic",
    "SubTopicName",
    _logger,
    waitForCompletion: true,  // Wait for sub-topic to complete
    _conversationContext));

// This executes AFTER sub-topic completes
Add(new SimpleActivity("after-sub", (ctx, input) => {
    var result = ctx.GetValue<object>("SubTopicCompletionData");
    // Continue main topic flow...
}));

// Complete topic properly
Add(new CompleteTopicActivity("complete", completionData, logger, context));
```

**Service Registration Pattern:**
Topics require explicit logger registration in DI:
```csharp
void AddLogger<T>(IServiceCollection svc) where T : class
    => svc.AddScoped(_ => _.GetRequiredService<ILoggerFactory>().CreateLogger<T>());

AddLogger<MyTopic>(services);
services.AddScoped<ITopic>(sp => new MyTopic(
    sp.GetRequiredService<TopicWorkflowContext>(),
    sp.GetRequiredService<ILogger<MyTopic>>(),
    sp.GetRequiredService<IConversationContext>()
));
```

**Important:** Topic registry configuration must occur in a service scope AFTER app build but BEFORE `app.Run()`.

### InsuranceSemanticV2 Data Layer

**Entity Framework Core:**
- 34 entities with explicit foreign keys (no shadow properties)
- All navigation properties are explicitly defined
- DbContext: `AppDbContext` in `InsuranceSemanticV2.Data/DataContext/`
- Entity configurations in `EntityConfigurations/` for non-standard primary keys
- One-to-one relationships use `IEntityTypeConfiguration<T>`

**Database Configuration:**
The API supports dual database modes via configuration:
- Production: SQL Server (`UseInMemoryDatabase = false`)
- Testing: InMemory (`UseInMemoryDatabase = true`)

This is configured in `Program.cs` and allows integration tests to use in-memory database without affecting production SQL Server.

**Entity Conventions:**
- Primary keys follow `{EntityName}Id` pattern (e.g., `LeadId`, `AgentId`)
- Non-standard PKs require explicit configuration in `EntityConfigurations/`
- Audit fields: `CreatedAt`, `UpdatedAt` (preserved during updates)
- Navigation properties use `List<T>` for collections

### API Architecture (Minimal API)

**Endpoint Organization:**
- Endpoints in `InsuranceSemanticV2.Api/Endpoints/` grouped by domain
- Each endpoint file contains a static extension method `Map{Domain}Endpoints()`
- All endpoints registered via `MapApiEndpoints()` in `Program.cs`

**SignalR Real-Time Updates:**
- Hub: `LeadsHub` in `InsuranceSemanticV2.Api/Hubs/`
- Hub URL: `/hubs/leads`
- Events: `LeadCreated`, `LeadUpdated`, `ProfileUpdated`, `KpisChanged`, `AgentConnected`, `AgentDisconnected`, `AgentStatusChanged`
- Registered in `Program.cs` via `builder.Services.AddSignalR()` and `app.MapHub<LeadsHub>("/hubs/leads")`
- CORS configured with `.AllowCredentials()` for WebSocket connections
- All lead/profile modification endpoints broadcast SignalR events after `SaveChangesAsync()`
- **Authentication:** JWT tokens passed via query string (`access_token` parameter) for WebSocket connections
- **Connection Tracking:** Hub tracks ConnectionId → AgentId mappings in static `ConcurrentDictionary`

**AutoMapper:**
- Configured via `AddAutoMapper(typeof(Program).Assembly)`
- Mapping profiles in `Mapping/` folder
- DTOs in `InsuranceSemanticV2.Core/DTO/`

**Response Pattern:**
- Base response wrapper: `BaseResponse<T>` with `Payload` property
- Specific responses inherit from base (e.g., `LeadResponse`, `AgentResponse`)
- HTTP status codes: 201 Created, 200 OK, 404 Not Found

## Integration Testing

**Test Structure:**
- Base class: `IntegrationTestBase` with `WebApplicationFactory<Program>`
- Each test gets isolated in-memory database instance
- Tests verify both API responses AND database state

**Running Integration Tests:**
```bash
cd InsuranceSemanticV2.IntegrationTests
dotnet test                                    # All tests
dotnet test --filter "LeadsEndpointsTests"    # Specific class
```

**Test Pattern:**
```csharp
public class MyEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task TestName()
    {
        // Arrange - seed database via DbContext
        var entity = new Entity { /* ... */ };
        DbContext.Entities.Add(entity);
        await DbContext.SaveChangesAsync();

        // Act - call API via Client
        var response = await Client.GetAsync("/api/endpoint");

        // Assert - verify HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - verify database state
        var dbEntity = await DbContext.Entities.FindAsync(id);
        dbEntity.Should().NotBeNull();
    }
}
```

## Agent Authentication & Session Tracking

**Overview:**
Demo-level JWT-based authentication system for LiveAgentConsole. Agents log in with email/password, receive JWT tokens, and maintain active sessions tracked in the database.

**⚠️ IMPORTANT:** This is a **demo-level authentication system** with plain-text password storage. NOT production-ready. Before production deployment, implement ASP.NET Core Identity, password hashing (bcrypt/Argon2), HTTPS enforcement, refresh tokens, and proper security measures.

**Demo Agents:**
Three test agents are seeded in the database:
- `agent1@test.com` / `demo123`
- `agent2@test.com` / `demo123`
- `licensed@test.com` / `demo123`

**Authentication Flow:**
1. **Login** (`POST /api/auth/login`):
   - Client sends username/password
   - Server validates against `Agents` table (plain text comparison)
   - Returns JWT token with claims: AgentId, Name, Email
   - Token stored in browser localStorage

2. **Session Start** (`POST /api/sessions/start`):
   - After SignalR connection established, client sends ConnectionId
   - Creates `AgentSession` record with Status = "Online", IsActive = true
   - Tracks IP address, user agent, login time

3. **Heartbeat** (`POST /api/sessions/heartbeat`):
   - Client sends heartbeat every 30 seconds
   - Updates `LastActivityTime` in database
   - Auto-updates status: Online → Away (5 min) → Offline (15 min)

4. **Logout** (`POST /api/auth/logout`):
   - Ends all active sessions (sets IsActive = false, LogoutTime = now)
   - Clears token from localStorage
   - Redirects to login page

**Background Services:**
- `SessionCleanupService`: Runs every 5 minutes to clean up stale sessions
  - Sets sessions to "Away" after 5 minutes of inactivity
  - Sets sessions to "Offline" and ends them after 15 minutes of inactivity
  - Configurable via `Session:AwayTimeoutMinutes` and `Session:OfflineTimeoutMinutes`

**Protected Endpoints:**
All endpoints requiring authentication use `[Authorize]` attribute and verify JWT token. Endpoints extract `AgentId` from claims via `ClaimsPrincipal.FindFirst("AgentId")`.

**LiveAgentConsole Integration:**
- `AuthService`: Handles login, logout, token management
- `AgentAuthenticationStateProvider`: Provides Blazor authentication state
- `SessionService`: Manages session lifecycle and heartbeat timer
- `AuthorizingHttpMessageHandler`: Automatically attaches Bearer token to HTTP requests
- Protected pages use `@attribute [Authorize]`

**Database Entities:**
- `AgentSession`: Tracks agent sessions with ConnectionId, LoginTime, LastActivityTime, LogoutTime, Status, IsActive
- `Agent.Password`: Plain text password field (demo only)

**JWT Configuration:**
Located in `appsettings.json`:
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

**Logging:**
All authentication events are logged with structured logging:
- Login attempts (successful and failed) with IP address
- Session starts/ends with AgentId and ConnectionId
- Logout events with session counts
- Token generation and validation errors
- Background cleanup service activity

**Integration Tests:**
- `AuthEndpointsTests.cs`: Tests login, logout, status updates, token validation
- `SessionEndpointsTests.cs`: Tests session start, heartbeat, end, active sessions query

## Configuration & Secrets

**OpenAI (Semantic Kernel):**
Add to `appsettings.Development.json`:
```json
{
  "OpenAI": {
    "ApiKey": "__ADD_KEY_HERE__",
    "Model": "gpt-4o-mini"
  }
}
```
System falls back to keyword-based responses if API key is not configured.

**Database Connection:**
SQL Server connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=InsuranceSemanticV2;..."
  }
}
```

## Key Design Patterns

### ConversaCore Patterns

1. **Event Bubbling:** Container activities (`ConditionalActivity`, `CompositeActivity`) implement `ITopicTriggeredActivity` and `ICustomEventTriggeredActivity` for event forwarding

2. **State Machine Transitions:** Configure valid state transitions explicitly:
   ```csharp
   _fsm.ConfigureTransition(FlowState.Starting, FlowState.CustomState);
   await _fsm.TryTransitionAsync(FlowState.CustomState);
   ```

3. **Activity Types:**
   - `AdaptiveCardActivity<TCard, TModel>`: Renders cards with validation
   - `TriggerTopicActivity`: Calls sub-topics (use `waitForCompletion: true` for hand-down pattern)
   - `CompleteTopicActivity`: Required for proper topic completion when using hand-down pattern
   - `EventTriggerActivity`: Triggers UI events (fire-and-forget or wait-for-response)
   - `DumpCtxActivity`: Debug context inspection (dev mode only)
   - `SimpleActivity`: Lambda-based inline logic

### InsuranceSemanticV2 Patterns

1. **Repository Pattern:** Not currently implemented - endpoints use DbContext directly

2. **DTO Mapping:** Manual mapping via extension methods or AutoMapper profiles

3. **Minimal API Groups:** Route groups with tags:
   ```csharp
   var group = routes.MapGroup("/api/domain").WithTags("Domain");
   ```

4. **No Shadow Properties:** All foreign keys are explicit properties in entities

5. **SignalR Broadcasting:** All endpoints that modify leads/profiles broadcast events:
   ```csharp
   // Inject IHubContext<LeadsHub>
   group.MapPost("/", async (LeadRequest req, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
       // ... save logic ...
       await db.SaveChangesAsync();
       
       // Broadcast to all connected clients
       await hubContext.Clients.All.SendAsync("LeadCreated", entity.LeadId);
       await hubContext.Clients.All.SendAsync("KpisChanged");
       
       return Results.Created(...);
   });
   ```

### LiveAgentConsole Patterns

1. **SignalR Connection Management:**
   - Service: `LeadHubConnection` (singleton) in `Services/`
   - Automatically reconnects on disconnect
   - Shared across components (Dashboard, LiveLeadTable)
   - Components subscribe/unsubscribe to events in lifecycle methods

2. **Real-Time UI Updates:**
   ```csharp
   @inject LeadHubConnection HubConnection
   @implements IAsyncDisposable
   
   protected override async Task OnInitializedAsync()
   {
       // Subscribe to events
       HubConnection.OnLeadCreated += HandleLeadChanged;
       HubConnection.OnProfileUpdated += HandleLeadChanged;
       
       // Start connection (idempotent - safe to call multiple times)
       await HubConnection.StartAsync();
   }
   
   private async Task HandleLeadChanged(int leadId)
   {
       await InvokeAsync(async () =>
       {
           await LoadData();
           StateHasChanged();
       });
   }
   
   public async ValueTask DisposeAsync()
   {
       HubConnection.OnLeadCreated -= HandleLeadChanged;
       HubConnection.OnProfileUpdated -= HandleLeadChanged;
   }
   ```

3. **No Polling:** LiveAgentConsole uses SignalR for all real-time updates (no HTTP polling timers)

## Common Pitfalls

1. **ConversaCore:**
   - Topic registry must be configured in service scope AFTER app build
   - Most services must be scoped, not singleton (context dependencies)
   - Use `CompleteTopicActivity` when using hand-down pattern
   - Event bubbling requires interface implementation on container activities

2. **Entity Framework:**
   - Non-standard primary key names require `IEntityTypeConfiguration<T>`
   - Always read file before editing when using Edit tool
   - Migration commands require both `--project` and `--startup-project` flags

3. **Integration Tests:**
   - Tests use in-memory database (configured via `UseInMemoryDatabase` flag)
   - Each test gets fresh database instance
   - InMemory provider doesn't support all SQL Server features

4. **SignalR:**
   - `LeadHubConnection` is singleton - shared across all components
   - Components must unsubscribe from events in `DisposeAsync()` to prevent memory leaks
   - Connection state checks recommended before `StartAsync()` (idempotent but efficient)
   - CORS must include `.AllowCredentials()` for WebSocket connections
   - All lead/profile modification endpoints MUST broadcast events after `SaveChangesAsync()`

## Service Lifetimes

**ConversaCore:**
- `TopicWorkflowContext`: Scoped
- `IConversationContext`: Scoped
- `TopicRegistry`: Singleton
- `Kernel` (Semantic): Singleton
- Topics: Scoped

**InsuranceSemanticV2:**
- `AppDbContext`: Scoped
- `IMapper`: Singleton
- API services: Typically scoped for request isolation
- `LeadHubConnection` (LiveAgentConsole): Singleton (shared SignalR connection)
