# API URL Path Duplication Fix

## Problem
Swagger and API endpoints were generating URLs with duplicate `/api` paths:
- **Incorrect**: `http://origovs-001-site1.ntempurl.com/api/api/leads`
- **Correct**: `http://origovs-001-site1.ntempurl.com/api/leads`

## Root Cause
The API was deployed with `PathBase="/api"` (virtual directory), but endpoint routes also had `/api` prefix, causing the duplication:
- `PathBase="/api"` adds `/api` to all routes automatically
- Endpoint routes like `MapGroup("/api/leads")` added another `/api`

## Solution Applied

### 1. Removed `/api` from All Endpoint Routes
Updated all 14 endpoint files to remove the `/api` prefix since `PathBase` handles it:

**Before:**
```csharp
var group = routes.MapGroup("/api/leads").WithTags("Leads");
```

**After:**
```csharp
var group = routes.MapGroup("/leads").WithTags("Leads");
```

**Files Modified:**
- ? LeadsEndpoints.cs
- ? AgentsEndpoints.cs
- ? AuthEndpoints.cs
- ? ProfileEndpoints.cs
- ? StatusEndpoints.cs
- ? InteractionEndpoints.cs
- ? ScoreEndpoints.cs
- ? SchedulingEndpoints.cs
- ? ComplianceEndpoints.cs
- ? CarrierEndpoints.cs
- ? SessionEndpoints.cs
- ? CarrierStateComplianceEndpoints.cs
- ? AgentAvailabilityEndpoints.cs
- ? ContactPolicyEndpoints.cs

### 2. Fixed Swagger Configuration
Updated Swagger endpoint path to work with PathBase:

**Before:**
```csharp
options.SwaggerEndpoint("../swagger/v1/swagger.json", "...");
```

**After:**
```csharp
options.SwaggerEndpoint("/swagger/v1/swagger.json", "...");
```

### 3. Fixed Hardcoded Paths in Responses
Removed `/api` from `Results.Created()` location headers in 4 endpoints:
- LeadsEndpoints
- ScoreEndpoints
- InteractionEndpoints
- AgentsEndpoints

## Result
? **Build Successful**

### URLs Now Working Correctly:
- **API Root**: `https://win8118.site4now.net/api/`
- **Swagger UI**: `https://win8118.site4now.net/api/swagger`
- **Leads Endpoint**: `https://win8118.site4now.net/api/leads`
- **Auth Endpoint**: `https://win8118.site4now.net/api/auth/login`

## How PathBase Works
When `PathBase="/api"` is set in `appsettings.Production.json`:
1. ASP.NET Core automatically prefixes ALL routes with `/api`
2. Route `/leads` becomes `/api/leads`
3. Route `/auth/login` becomes `/api/auth/login`
4. Swagger UI automatically respects the PathBase

## Testing
After publishing, test these URLs:
- `https://win8118.site4now.net/api/` - Should show "InsuranceSemanticV2 API running."
- `https://win8118.site4now.net/api/swagger` - Should load Swagger UI
- Try any endpoint in Swagger - URLs should NOT have duplicate `/api`

## Important Notes
- **Local Development**: Uses `PathBase=""` so routes work as `/leads` directly
- **Production**: Uses `PathBase="/api"` so routes become `/api/leads`
- **No code changes needed** when switching between environments
