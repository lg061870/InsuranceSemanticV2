# URLs Updated to Use Temp Domain

## ? All URLs Changed From:
- ? `win8118.site4now.net` (not accessible)

## ? To Temp URL:
- ? `origovs-001-site1.ntempurl.com` (accessible now)

---

## Files Updated:

### **1. Production Configuration Files**
? `InsuranceAgent/appsettings.Production.json`
   - API BaseUrl: `https://origovs-001-site1.ntempurl.com/api/`

? `LiveAgentConsole/wwwroot/appsettings.Production.json`
   - API BaseUrl: `https://origovs-001-site1.ntempurl.com/api/`

### **2. CORS Configuration**
? `InsuranceSemanticV2.Api/Program.cs`
   - Added temp domain to allowed origins:
     - `https://origovs-001-site1.ntempurl.com`
     - `https://origovs-001-site1.ntempurl.com/liveagent`

### **3. Publish Profiles (Launch URLs)**
? `InsuranceSemanticV2.Api/Properties/PublishProfiles/SmarterASP-API.pubxml`
   - Launch URL: `https://origovs-001-site1.ntempurl.com/api`

? `InsuranceAgent/Properties/PublishProfiles/SmarterASP-Root.pubxml`
   - Launch URL: `https://origovs-001-site1.ntempurl.com`

? `LiveAgentConsole/Properties/PublishProfiles/SmarterASP-LiveAgent.pubxml`
   - Launch URL: `https://origovs-001-site1.ntempurl.com/liveagent`

? `SimpleBlazorDemo/Properties/PublishProfiles/SmarterASP-BlazorDemo.pubxml`
   - Launch URL: `https://origovs-001-site1.ntempurl.com/blazordemo`

---

## ?? Ready to Publish!

### Your Deployment URLs:
1. **API**: `https://origovs-001-site1.ntempurl.com/api/`
2. **InsuranceAgent**: `https://origovs-001-site1.ntempurl.com/`
3. **LiveAgentConsole**: `https://origovs-001-site1.ntempurl.com/liveagent`
4. **SimpleBlazorDemo**: `https://origovs-001-site1.ntempurl.com/blazordemo`

### Test After Publishing:
- **API Swagger**: `https://origovs-001-site1.ntempurl.com/api/swagger`
- **API Root**: `https://origovs-001-site1.ntempurl.com/api/`
- **SignalR Hub**: `https://origovs-001-site1.ntempurl.com/api/hubs/leads`

---

## ?? When Your Domain Becomes Accessible

When `win8118.site4now.net` becomes accessible, you'll need to update:
1. `InsuranceAgent/appsettings.Production.json` - API BaseUrl
2. `LiveAgentConsole/wwwroot/appsettings.Production.json` - API BaseUrl
3. `InsuranceSemanticV2.Api/Program.cs` - CORS origins
4. Optionally: Publish profile launch URLs (just for convenience)

Then republish all 4 projects.

---

## ? Build Status: **SUCCESSFUL**
All changes compiled successfully. Ready to publish!
