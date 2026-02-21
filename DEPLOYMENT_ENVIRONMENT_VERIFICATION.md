# How to Verify Production Configuration When Publishing

## Quick Verification Checklist

### ? Before Publishing (In Visual Studio):
1. **Build Configuration Dropdown** (top toolbar)
   - Should show `Release` (not `Debug`)
   
2. **Publish Dialog** (when you click Publish)
   - **Configuration:** `Release`
   - **Target Framework:** `net9.0`
   - **Deployment Mode:** Framework-dependent

### ? What Gets Published:

#### 1. **Build Configuration** (from publish profile):
- `<LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>`
- Optimized code, no debug symbols

#### 2. **Environment Variable** (from web.config):
- `ASPNETCORE_ENVIRONMENT = Production`
- Loads `appsettings.Production.json`

#### 3. **Configuration Files Deployed**:
- ? `appsettings.json` (base settings)
- ? `appsettings.Production.json` (production overrides)
- ? `appsettings.Development.json` (NOT included in Release build)

## How ASP.NET Core Loads Configuration

The app loads configs in this order (later ones override earlier):
1. `appsettings.json`
2. `appsettings.{Environment}.json` (based on ASPNETCORE_ENVIRONMENT)
3. Environment variables
4. Command-line arguments

### Example for Your API:
When `ASPNETCORE_ENVIRONMENT=Production`:
```
appsettings.json              ? PathBase = ""
appsettings.Production.json   ? PathBase = "/api" (OVERRIDES)
Final Result                  ? PathBase = "/api" ?
```

## Verification After Deployment

### 1. Check the Environment in Logs:
Look at your `logs\stdout` file on the server, you should see:
```
info: Microsoft.Hosting.Lifetime[0]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Environment: Production
```

### 2. Test Swagger Configuration:
- Production Swagger URL: `https://win8118.site4now.net/api/swagger`
- If `EnableSwagger: true` in `appsettings.Production.json`, it will load
- Development settings won't be used

### 3. Check Database Connection:
- Production uses: `SQL8004.site4now.net` database
- Development uses: `localhost\SQLEXPRESS`
- If wrong environment, database connection will fail

## Common Issues

### ? Problem: Development Settings Being Used
**Symptoms:**
- PathBase is empty (no `/api` prefix)
- Connects to localhost database
- Different API behavior

**Cause:**
- `ASPNETCORE_ENVIRONMENT` not set correctly in `web.config`

**Fix:**
- Verify `web.config` has: `<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />`

### ? Problem: Old Files Not Replaced
**Symptoms:**
- Changes not reflected after publish

**Cause:**
- `SkipExtraFilesOnServer=true` only adds new files, doesn't replace

**Fix:**
- In publish profile settings, check **"Remove additional files at destination"**

## Force Visual Studio to Use Release Configuration

### Option 1: Set in Visual Studio
1. Top toolbar ? Configuration dropdown ? Select **Release**
2. Right-click publish profile ? **Publish**

### Option 2: Edit Publish Profile
Add this to your `.pubxml`:
```xml
<PropertyGroup>
  <Configuration>Release</Configuration>
</PropertyGroup>
```

## Verify Production Configuration Files Are Correct

### Check Your Files Have Production Values:

**? InsuranceSemanticV2.Api/appsettings.Production.json:**
- `PathBase: "/api"` (not empty)
- Database: `SQL8004.site4now.net` (not localhost)
- JWT SecretKey: Secure random key (not demo key)

**? InsuranceAgent/appsettings.Production.json:**
- `ApiSettings.BaseUrl: "https://win8118.site4now.net/api/"` (not localhost)

**? LiveAgentConsole/wwwroot/appsettings.Production.json:**
- `ApiSettings.BaseUrl: "https://win8118.site4now.net/api/"` (not localhost)

## Pro Tip: Use Configuration Transform
You can verify which config is loaded by adding this to `Program.cs`:

```csharp
var app = builder.Build();

// Log which environment and config is loaded
app.Logger.LogInformation("Environment: {env}", app.Environment.EnvironmentName);
app.Logger.LogInformation("PathBase: {path}", app.Configuration["PathBase"]);
app.Logger.LogInformation("Database: {db}", app.Configuration.GetConnectionString("DefaultConnection"));
```

Then check the `logs\stdout` file after deployment to confirm.
