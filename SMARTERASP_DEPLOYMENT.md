# SmarterASP.NET Deployment Guide

## Deployment Structure

This solution deploys 4 applications to a single SmarterASP.NET account:

| Project | URL | Type | Path |
|---------|-----|------|------|
| **InsuranceAgent** | https://win8118.site4now.net | Blazor Server (Root) | / |
| **SimpleBlazorDemo** | https://win8118.site4now.net/blazordemo | Blazor Server | /blazordemo |
| **LiveAgentConsole** | https://win8118.site4now.net/livenagent | Blazor WebAssembly | /livenagent |
| **InsuranceSemanticV2.Api** | https://win8118.site4now.net/api | ASP.NET Core API | /api |

## Deployment Steps

### Prerequisites
- Visual Studio 2022
- SmarterASP.NET account credentials
- Web Deploy configured in SmarterASP.NET control panel

### Publishing Order (Recommended)

Publish in this order to avoid deployment conflicts:

#### 1. API (Deploy First)
```bash
# In Visual Studio:
# Right-click InsuranceSemanticV2.Api > Publish
# Select profile: SmarterASP-API
# Fill in username when prompted
```

#### 2. InsuranceAgent (Root Application)
```bash
# Right-click InsuranceAgent > Publish
# Select profile: SmarterASP-Root
# Fill in username when prompted
```

#### 3. SimpleBlazorDemo (Virtual Directory)
```bash
# Right-click SimpleBlazorDemo > Publish
# Select profile: SmarterASP-BlazorDemo
# Fill in username when prompted
```

#### 4. LiveAgentConsole (Virtual Directory)
```bash
# Right-click LiveAgentConsole > Publish
# Select profile: SmarterASP-LiveAgent
# Fill in username when prompted
```

## Configuration Files

### InsuranceAgent
- **Development**: `appsettings.json` - Points to `http://localhost:5031/`
- **Production**: `appsettings.Production.json` - Points to `https://win8118.site4now.net/api/`

### InsuranceSemanticV2.Api
- **Development**: `appsettings.json` - No path base
- **Production**: `appsettings.Production.json` - PathBase set to `/api`

## Important Notes

1. **Publish Profiles**: Update `YOUR_USERNAME` in each `.pubxml` file with your SmarterASP.NET credentials
2. **CORS Configuration**: The API is configured to accept requests from all deployed sites
3. **PathBase**: The API uses `UsePathBase("/api")` to handle virtual directory routing
4. **Database**: Ensure SQL Server connection strings are correct for your hosting environment
5. **SSL**: SmarterASP.NET provides free SSL certificates by default

## Troubleshooting

### Issue: Virtual directory apps not loading
- **Solution**: Ensure the IIS virtual application is created in SmarterASP.NET control panel before deploying

### Issue: API calls fail from Blazor apps
- **Solution**: Check browser console for CORS errors. Verify API base URL in appsettings.Production.json

### Issue: SignalR connections fail
- **Solution**: Ensure WebSocket support is enabled in SmarterASP.NET. CORS credentials are configured in the API.

### Issue: Static files not loading in virtual directories
- **Solution**: Check that `UsePathBase()` is called before static file middleware in Program.cs

## Testing After Deployment

1. **InsuranceAgent**: https://win8118.site4now.net - Full chat interface
2. **SimpleBlazorDemo**: https://win8118.site4now.net/blazordemo - Demo page
3. **LiveAgentConsole**: https://win8118.site4now.net/livenagent - WebAssembly app
4. **API Health**: https://win8118.site4now.net/api/ - Should show "InsuranceSemanticV2 API running."

## Updating Deployments

To update any application after initial deployment:
1. Right-click the project → Publish
2. Select the appropriate SmarterASP profile
3. Visual Studio will handle the update and preserve settings

## Rollback

If deployment fails:
1. Go to SmarterASP.NET control panel → Database Backups
2. Restore a previous version if available
3. Or redeploy the last working build
