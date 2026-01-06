# LiveAgentConsole Real-Time Lead Monitoring - Integration Plan

## Executive Summary
Connect the LiveAgentConsole (Blazor WebAssembly) to the InsuranceSemanticV2.Api to display real-time lead information as customers progress through the T1Topic conversation flow in InsuranceAgent.

## Current State Analysis

### LiveAgentConsole Structure
- **Type**: Blazor WebAssembly (client-side)
- **Port**: https://localhost:7089 / http://localhost:5033
- **API Configuration**: Currently points to `https://localhost:7097/` (❌ WRONG - should be `http://localhost:5031/`)
- **Components**:
  - `Dashboard.razor` - Main layout with ActiveCallCard, LiveLeadTable, CopilotContext
  - `LiveLeadTable.razor` - Table showing leads (currently hardcoded example)
  - `LeadRowView.cs` - ViewModel for lead display
  - `LeadService.cs` - Service to fetch leads from API (partially implemented)

### Existing API Endpoints
- ✅ `GET /api/leads/{leadId}` - Get single lead
- ✅ `PUT /api/profile/{leadId}/contact-info` - Save contact info
- ✅ `PUT /api/profile/{leadId}/health` - Save health info
- ✅ `PUT /api/profile/{leadId}/goals` - Save life goals
- ✅ `PUT /api/profile/{leadId}/coverage` - Save coverage intent
- ✅ `PUT /api/profile/{leadId}/dependents` - Save dependents
- ✅ `PUT /api/profile/{leadId}/employment` - Save employment
- ✅ `PUT /api/profile/{leadId}/beneficiary` - Save beneficiary info

### Missing Components
- ❌ `GET /api/leads` - List all leads endpoint
- ❌ Real-time update mechanism (SignalR or polling)
- ❌ Lead progress tracking (which adaptive cards completed)
- ❌ Actual binding of data in LiveLeadTable.razor
- ❌ KPI calculations (Dashboard metrics)
- ❌ Lead filtering and search
- ❌ Auto-refresh mechanism

## Implementation Plan

### Phase 1: Fix API Configuration & Add List Endpoint ⭐ PRIORITY

#### 1.1 Fix API Base URL in LiveAgentConsole
**File**: `LiveAgentConsole/Program.cs`
**Change**: Line 14
```csharp
// BEFORE:
client.BaseAddress = new Uri("https://localhost:7097/");

// AFTER:
client.BaseAddress = new Uri("http://localhost:5031/");
```

#### 1.2 Add GET /api/leads Endpoint
**File**: `InsuranceSemanticV2.Api/Endpoints/LeadsEndpoints.cs`
**Add new endpoint**:
```csharp
group.MapGet("/", async (AppDbContext db) => {
    var leads = await db.Leads
        .Include(l => l.Profile)
            .ThenInclude(p => p.ContactInfo)
        .Include(l => l.Profile)
            .ThenInclude(p => p.HealthInfo)
        .Include(l => l.Profile)
            .ThenInclude(p => p.LifeGoals)
        .OrderByDescending(l => l.CreatedAt)
        .ToListAsync();

    var leadRequests = leads.Select(l => new LeadRequest {
        FullName = l.FullName ?? l.Profile?.ContactInfo?.FullName ?? "Unknown",
        Email = l.Email ?? l.Profile?.ContactInfo?.EmailAddress,
        Phone = l.Phone ?? l.Profile?.ContactInfo?.PhoneNumber,
        Status = l.Status,
        LeadSource = l.LeadSource,
        QualificationScore = l.QualificationScore,
        AssignedAgentId = l.AssignedAgentId,
        Notes = l.Notes,
        InterestLevel = l.InterestLevel,
        // Add more fields as needed
    }).ToList();

    return Results.Ok(new LeadResponse {
        Payload = leadRequests
    });
});
```

### Phase 2: Enhanced Lead ViewModel with Profile Data

#### 2.1 Create LeadDetailViewModel
**New File**: `LiveAgentConsole/ViewModels/LeadDetailViewModel.cs`
```csharp
public class LeadDetailViewModel {
    public int LeadId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Status { get; set; }
    public int QualificationScore { get; set; }

    // Progress tracking
    public bool HasContactInfo { get; set; }
    public bool HasHealthInfo { get; set; }
    public bool HasLifeGoals { get; set; }
    public bool HasCoverageIntent { get; set; }
    public bool HasDependents { get; set; }
    public bool HasEmployment { get; set; }
    public bool HasBeneficiary { get; set; }

    public int ProgressPercentage => CalculateProgress();
    public string ProgressStage => DetermineStage();

    // Contact details
    public DateTime? DateOfBirth { get; set; }
    public string City { get; set; }
    public string State { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public TimeSpan TimeInSystem => DateTime.Now - CreatedAt;
}
```

#### 2.2 Add GET /api/leads/{id}/details Endpoint
**File**: `InsuranceSemanticV2.Api/Endpoints/LeadsEndpoints.cs`
```csharp
group.MapGet("/{leadId:int}/details", async (int leadId, AppDbContext db) => {
    var lead = await db.Leads
        .Include(l => l.Profile)
            .ThenInclude(p => p.ContactInfo)
        .Include(l => l.Profile)
            .ThenInclude(p => p.HealthInfo)
        .Include(l => l.Profile)
            .ThenInclude(p => p.LifeGoals)
        .Include(l => l.Profile)
            .ThenInclude(p => p.CoverageIntent)
        .Include(l => l.Profile)
            .ThenInclude(p => p.Dependents)
        .Include(l => l.Profile)
            .ThenInclude(p => p.Employment)
        .Include(l => l.Profile)
            .ThenInclude(p => p.BeneficiaryInfo)
        .FirstOrDefaultAsync(l => l.LeadId == leadId);

    if (lead == null) return Results.NotFound();

    var detail = new LeadDetailViewModel {
        LeadId = lead.LeadId,
        FullName = lead.FullName ?? lead.Profile?.ContactInfo?.FullName,
        Email = lead.Email ?? lead.Profile?.ContactInfo?.EmailAddress,
        Phone = lead.Phone ?? lead.Profile?.ContactInfo?.PhoneNumber,
        Status = lead.Status,
        QualificationScore = lead.QualificationScore ?? 0,

        HasContactInfo = lead.Profile?.ContactInfo != null,
        HasHealthInfo = lead.Profile?.HealthInfo != null,
        HasLifeGoals = lead.Profile?.LifeGoals != null,
        HasCoverageIntent = lead.Profile?.CoverageIntent != null,
        HasDependents = lead.Profile?.Dependents != null,
        HasEmployment = lead.Profile?.Employment != null,
        HasBeneficiary = lead.Profile?.BeneficiaryInfo != null,

        CreatedAt = lead.CreatedAt,
        UpdatedAt = lead.UpdatedAt,
    };

    return Results.Ok(detail);
});
```

### Phase 3: Real-Time Updates (Choose One Approach)

#### Option A: Polling (Simpler, Good for MVP)
**File**: `LiveAgentConsole/Pages/Components/LiveLeadTable.razor`
```csharp
@code {
    private System.Threading.Timer? _refreshTimer;
    private List<LeadRowView> leads = new();

    protected override async Task OnInitializedAsync() {
        await RefreshLeads();

        // Refresh every 5 seconds
        _refreshTimer = new Timer(async _ => {
            await InvokeAsync(async () => {
                await RefreshLeads();
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private async Task RefreshLeads() {
        leads = await LeadService.GetAllLeadsAsync();
    }

    public void Dispose() {
        _refreshTimer?.Dispose();
    }
}
```

#### Option B: SignalR (More Efficient, Real-Time)
**Requires**:
1. Add `Microsoft.AspNetCore.SignalR.Client` to LiveAgentConsole
2. Create Hub in API: `LeadsHub.cs`
3. Broadcast updates when leads are created/updated
4. Subscribe to hub in LiveLeadTable component

**Recommendation**: Start with Option A (Polling) for MVP, migrate to Option B later.

### Phase 4: UI Enhancements

#### 4.1 Update LiveLeadTable.razor to Display Real Data
```razor
@inject LeadService LeadService
@implements IDisposable

<div class="flex-1 px-6 pb-6 overflow-hidden">
    <div class="bg-white border border-gray-200 rounded-lg shadow-sm h-full overflow-hidden flex flex-col">
        <div class="overflow-y-auto flex-1">
            <table class="w-full text-left border-collapse">
                <thead class="bg-gray-50 sticky top-0 z-10">
                    <tr>
                        <th class="px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-200">Lead Name</th>
                        <th class="px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-200">Product</th>
                        <th class="px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-200 text-center">Score</th>
                        <th class="px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-200">Progress</th>
                        <th class="px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-200">Status</th>
                        <th class="px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-200 text-right">Actions</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                    @if (leads.Any()) {
                        @foreach (var lead in leads) {
                            <LeadRow Lead="@lead" />
                        }
                    } else {
                        <tr>
                            <td colspan="6" class="px-6 py-8 text-center text-gray-500">
                                No leads available. Waiting for customers...
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
</div>

@code {
    private List<LeadRowView> leads = new();
    private System.Threading.Timer? _refreshTimer;

    protected override async Task OnInitializedAsync() {
        await RefreshLeads();
        _refreshTimer = new Timer(async _ => {
            await InvokeAsync(async () => {
                await RefreshLeads();
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private async Task RefreshLeads() {
        try {
            leads = await LeadService.GetAllLeadsAsync();
        } catch (Exception ex) {
            // Log error, show toast notification
            Console.WriteLine($"Error refreshing leads: {ex.Message}");
        }
    }

    public void Dispose() {
        _refreshTimer?.Dispose();
    }
}
```

#### 4.2 Create LeadRow Component
**File**: `LiveAgentConsole/Pages/Components/LeadRow.razor`
```razor
@using LiveAgentConsole.ViewModels

<tr class="hover:bg-blue-50/50 transition-colors group cursor-pointer" @onclick="() => OnLeadClick(Lead.Id)">
    <td class="px-6 py-4">
        <div class="flex items-center gap-3">
            <img src="@Lead.AvatarUrl" class="w-8 h-8 rounded-full" alt="@Lead.Name" />
            <div>
                <div class="font-medium text-gray-900">@Lead.Name</div>
                <div class="text-sm text-gray-500">@Lead.Email</div>
            </div>
        </div>
    </td>
    <td class="px-6 py-4">
        <div class="flex items-center gap-2">
            <i class="@Lead.ProductIcon text-blue-600"></i>
            <div>
                <div class="text-sm font-medium">@Lead.Product</div>
                <div class="text-xs text-gray-500">@Lead.SubProduct</div>
            </div>
        </div>
    </td>
    <td class="px-6 py-4 text-center">
        <div class="inline-flex items-center gap-2">
            <div class="relative w-12 h-12">
                <svg class="transform -rotate-90 w-12 h-12">
                    <circle cx="24" cy="24" r="20" stroke="#e5e7eb" stroke-width="4" fill="none"/>
                    <circle cx="24" cy="24" r="20" stroke="@Lead.ScoreColor" stroke-width="4" fill="none"
                            stroke-dasharray="@(2 * Math.PI * 20)"
                            stroke-dashoffset="@(2 * Math.PI * 20 * Lead.ScoreOffset / 100)"
                            stroke-linecap="round"/>
                </svg>
                <div class="absolute inset-0 flex items-center justify-center text-xs font-bold">
                    @Lead.Score
                </div>
            </div>
            <span class="@Lead.ScoreLabelColor text-xs font-semibold">@Lead.ScoreLabel</span>
        </div>
    </td>
    <td class="px-6 py-4">
        <div class="flex items-center gap-2">
            <div class="flex-1 bg-gray-200 rounded-full h-2 overflow-hidden">
                <div class="bg-blue-600 h-full rounded-full transition-all" style="width: @(Lead.ProgressPercentage)%"></div>
            </div>
            <span class="text-xs text-gray-600">@Lead.ProgressPercentage%</span>
        </div>
    </td>
    <td class="px-6 py-4">
        <span class="@GetStatusClass(Lead.Status) px-2 py-1 text-xs font-medium rounded-full">
            @Lead.Status
        </span>
    </td>
    <td class="px-6 py-4 text-right text-sm font-medium">
        <button class="text-blue-600 hover:text-blue-800 bg-blue-50 hover:bg-blue-100 px-3 py-1 rounded transition-colors mr-2">
            View
        </button>
    </td>
</tr>

@code {
    [Parameter] public LeadRowView Lead { get; set; } = null!;

    private void OnLeadClick(int leadId) {
        // TODO: Navigate to lead detail view or open modal
    }

    private string GetStatusClass(string status) => status switch {
        "New" => "bg-green-100 text-green-800",
        "Contacted" => "bg-blue-100 text-blue-800",
        "Qualified" => "bg-purple-100 text-purple-800",
        "Unqualified" => "bg-red-100 text-red-800",
        _ => "bg-gray-100 text-gray-800"
    };
}
```

### Phase 5: KPI Dashboard

#### 5.1 Add KPI Endpoint
**File**: `InsuranceSemanticV2.Api/Endpoints/LeadsEndpoints.cs`
```csharp
group.MapGet("/kpis", async (AppDbContext db) => {
    var today = DateTime.Today;

    var kpis = new {
        TotalLeads = await db.Leads.CountAsync(),
        NewToday = await db.Leads.CountAsync(l => l.CreatedAt >= today),
        Qualified = await db.Leads.CountAsync(l => l.QualificationScore >= 70),
        InProgress = await db.Leads.CountAsync(l => l.Status == "New" || l.Status == "Contacted"),
        AverageScore = await db.Leads.Where(l => l.QualificationScore.HasValue)
                                      .AverageAsync(l => l.QualificationScore.Value)
    };

    return Results.Ok(kpis);
});
```

#### 5.2 Update DashboardKpiAndFilters Component
Display real KPIs instead of hardcoded values.

### Phase 6: Progress Tracking Enhancement

#### 6.1 Update LeadRowView with Progress Calculation
**File**: `LiveAgentConsole/ViewModels/LeadRowView.cs`
```csharp
public class LeadRowView {
    // ... existing properties ...

    // Progress tracking
    public bool HasContactInfo { get; set; }
    public bool HasHealthInfo { get; set; }
    public bool HasLifeGoals { get; set; }
    public bool HasCoverageIntent { get; set; }
    public bool HasDependents { get; set; }
    public bool HasEmployment { get; set; }
    public bool HasBeneficiary { get; set; }

    public int ProgressPercentage {
        get {
            var completed = 0;
            var total = 7; // Total adaptive cards in T1Topic

            if (HasContactInfo) completed++;
            if (HasHealthInfo) completed++;
            if (HasLifeGoals) completed++;
            if (HasCoverageIntent) completed++;
            if (HasDependents) completed++;
            if (HasEmployment) completed++;
            if (HasBeneficiary) completed++;

            return (int)((double)completed / total * 100);
        }
    }

    public string ProgressStage {
        get {
            if (ProgressPercentage < 20) return "Just Started";
            if (ProgressPercentage < 40) return "Contact Info";
            if (ProgressPercentage < 60) return "Health & Goals";
            if (ProgressPercentage < 80) return "Coverage Details";
            return "Almost Done";
        }
    }
}
```

#### 6.2 Update LeadService to Map Progress
**File**: `LiveAgentConsole/Services/LeadService.cs`
```csharp
public async Task<List<LeadRowView>> GetAllLeadsAsync() {
    var response = await http.GetFromJsonAsync<LeadResponse>("api/leads");
    if (response?.Payload == null) return [];

    // For each lead, fetch detailed progress
    var leadViews = new List<LeadRowView>();

    foreach (var lead in response.Payload) {
        var detail = await http.GetFromJsonAsync<LeadDetailViewModel>($"api/leads/{lead.LeadId}/details");

        leadViews.Add(new LeadRowView {
            Id = lead.LeadId,
            Name = lead.FullName ?? "Unknown",
            Email = lead.Email ?? "",
            Status = lead.Status ?? "New",
            Score = lead.QualificationScore ?? 50,

            // Progress from detailed endpoint
            HasContactInfo = detail?.HasContactInfo ?? false,
            HasHealthInfo = detail?.HasHealthInfo ?? false,
            HasLifeGoals = detail?.HasLifeGoals ?? false,
            HasCoverageIntent = detail?.HasCoverageIntent ?? false,
            HasDependents = detail?.HasDependents ?? false,
            HasEmployment = detail?.HasEmployment ?? false,
            HasBeneficiary = detail?.HasBeneficiary ?? false,

            // ... other properties
        });
    }

    return leadViews;
}
```

## Implementation Priority

### ⭐ Phase 1 (CRITICAL - Do First)
1. Fix API base URL in Program.cs
2. Add `GET /api/leads` endpoint
3. Update LiveLeadTable to use real data with polling

### ⭐⭐ Phase 2 (HIGH)
4. Add `GET /api/leads/{id}/details` endpoint
5. Update LeadRowView with progress tracking
6. Create LeadRow component with progress visualization

### ⭐⭐⭐ Phase 3 (MEDIUM)
7. Add KPI endpoint
8. Update DashboardKpiAndFilters with real data
9. Add filtering and search

### Phase 4 (NICE TO HAVE)
10. Migrate to SignalR for real-time updates
11. Add lead detail modal/page
12. Add agent assignment features

## Testing Strategy

1. **Manual Testing**:
   - Start API, InsuranceAgent, and LiveAgentConsole
   - Create a lead through InsuranceAgent (T1Topic flow)
   - Verify lead appears in LiveAgentConsole within 5 seconds
   - Fill out adaptive cards and verify progress updates

2. **Integration Tests**:
   - Test GET /api/leads returns all leads
   - Test GET /api/leads/{id}/details returns complete profile
   - Test KPI calculations

## Expected Behavior After Implementation

1. Agent opens LiveAgentConsole at http://localhost:5033
2. Sees empty table: "No leads available. Waiting for customers..."
3. Customer starts conversation in InsuranceAgent
4. Within 5 seconds, new lead appears in LiveAgentConsole with:
   - Name: "Unknown" (until contact info submitted)
   - Status: "New"
   - Progress: 0%
   - Score: 0 (or default)
5. As customer fills adaptive cards:
   - Name updates when contact info submitted
   - Progress bar increases (14% per card)
   - Score updates based on QualificationScore
   - Status can be manually updated by agents
6. KPI cards update:
   - Total leads count increases
   - "New Today" increases
   - Average score recalculates

## Dependencies & Prerequisites

- ✅ InsuranceSemanticV2.Core (DTOs)
- ✅ InsuranceSemanticV2.Data (Entities, DbContext)
- ✅ InsuranceSemanticV2.Api (Endpoints)
- ❌ Need to add project references to LiveAgentConsole.csproj

## Rollout Plan

1. Create feature branch: `feature/liveagent-integration`
2. Implement Phase 1 (API fix + list endpoint)
3. Test with existing leads in database
4. Implement Phase 2 (progress tracking)
5. Test full flow: InsuranceAgent → API → LiveAgentConsole
6. Implement Phase 3 (KPIs)
7. Deploy to staging
8. User acceptance testing
9. Merge to main

## Notes

- LiveAgentConsole is Blazor WebAssembly (runs in browser), so all data must come from API
- Cannot access DbContext directly from LiveAgentConsole
- All database queries must go through API endpoints
- Consider adding caching to reduce API calls
- Monitor performance with multiple concurrent leads
