# ConversaCore SDK

Developer tools and project templates for building ConversaCore-based conversational AI applications.

## Contents

### 1. ConversaCore.BlazorTemplateHost
A .NET project template for creating new ConversaCore Blazor Server applications.

**Features:**
- Pre-configured Blazor Server with ConversaCore integration
- All system topics registered (FallbackTopic, SignInTopic, ResetConversationTopic, etc.)
- Sample topic with adaptive cards demonstrating the framework
- Proper DI setup following ConversaCore patterns
- Ready-to-use domain agent service

**Installation:**
```powershell
cd ConversaCore.BlazorTemplateHost
.\tools\build-template.ps1 -Install
```

**Usage:**
```bash
dotnet new conversacore-blazor -o C:\MyNewApp
```

### 2. ConversaCore.TopicSimulator
Console harness for testing individual TopicFlow instances in isolation.

**Usage:**
```bash
# Run with defaults (executes SampleTopic)
dotnet run --project ConversaCore.TopicSimulator

# Run a specific topic
dotnet run --project ConversaCore.TopicSimulator -- \
    "C:\path\to\assembly.dll" "Namespace.TopicName"
```

**Purpose:**
- Rapid topic development without full app startup
- Isolated testing of topic logic
- Console-based debugging with workflow context snapshots

### 3. ConversaCore.TopicTool.VSIX
Visual Studio extension for ConversaCore topic development (separate project).

## Template Management

### Uninstall Previous Version
```powershell
# List installed templates
dotnet new uninstall

# Uninstall the ConversaCore template
dotnet new uninstall "C:\Users\lg061\source\repos\InsuranceSemanticV2\templates\conversacore-blazor"
```

### Rebuild and Install
```powershell
cd ConversaCore.BlazorTemplateHost
.\tools\build-template.ps1 -Install
```

### Verify Installation
```powershell
dotnet new list | Select-String "conversacore"
```

## Template Structure

The Blazor template includes:

```
ConversaCore.BlazorTemplateHost/
├── Configuration/
│   └── ConversaCoreTopicRegistration.cs  # Central topic registration
├── Services/
│   └── MyDomainAgentService.cs           # Domain-specific agent
├── Topics/
│   └── SampleTopic/                      # Sample topic with cards
├── Pages/
│   └── Index.razor                       # Main page with chat UI
├── lib/
│   ├── ConversaCore.dll                  # Framework DLLs
│   └── ConversaCore.UI.dll
└── templates/
    └── conversacore-blazor/
        └── .template.config/
            └── template.json             # Template definition
```

## Dependencies

The template project includes all required NuGet packages:
- Microsoft.SemanticKernel.Connectors.* (1.66.0)
- Microsoft.Data.Sqlite (9.0.10)
- Microsoft.Extensions.AI (9.10.1)
- SQLitePCLRaw.bundle_e_sqlite3 (3.0.2)
- UglyToad.PdfPig (1.7.0-custom-5)

## Development Notes

### Why .NET Template Format?
We use the .NET CLI templating engine (`.template.config/template.json`) instead of Visual Studio's legacy "Export Template" feature because:

✅ Cross-platform (VS, VS Code, CLI)  
✅ More powerful (symbol replacement, conditional content)  
✅ Actively maintained by Microsoft  
✅ Can distribute as NuGet packages  
✅ Works without VS dependency  

### Template Exclusions
The template automatically excludes:
- Build artifacts (`bin/`, `obj/`, `.vs/`)
- Solution files (`.sln`)
- Old VS template files (`.vstemplate`)
- Template tools and artifacts
- Custom build configuration (`Directory.Build.props`)

### Updating DLLs
When you build the main ConversaCore project, it automatically copies the latest DLLs to `ConversaCore.BlazorTemplateHost/lib/` via a post-build target.

## Troubleshooting

**Build Errors:**
```powershell
# Clean and restore
cd ConversaCore.BlazorTemplateHost
dotnet clean
dotnet restore
dotnet build
```

**Template Not Appearing:**
- Check installation: `dotnet new list`
- Restart Visual Studio
- Uninstall and reinstall the template

**Missing Dependencies:**
- Ensure `lib/ConversaCore.dll` and `lib/ConversaCore.UI.dll` exist
- Run `dotnet restore` in the template project

## Contributing

When modifying the template:

1. Make changes in `ConversaCore.BlazorTemplateHost/`
2. Test the project builds: `dotnet build`
3. Update template exclusions in `templates/conversacore-blazor/.template.config/template.json` if needed
4. Rebuild template: `.\tools\build-template.ps1`
5. Test template creation: `dotnet new conversacore-blazor -o C:\temp\TestApp`
6. Verify the generated project compiles

---

For more information, see the main repository documentation at `/docs/ConversaCore.TopicAuthoringGuide.md`.
