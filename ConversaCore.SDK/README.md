# ConversaCore SDK

Developer tools and project templates for building ConversaCore-based conversational AI applications.

## Contents

### 1. ConversaCore.BlazorTemplateHost
A .NET project template for creating new ConversaCore Blazor Server applications.

**Features:**
- Pre-configured Blazor Server with ConversaCore integration
- Descriptor-based sample-topic registration with a stable start ID
- Scoped `IConversationRuntime` wired directly to ConversaCore.UI
- Sample topics:
  - `SampleTopic`: Minimal composed flow demonstrating prompt, quick answer, generated adaptive card, fallback, and completion
  - `SampleToolTopic`: Bounded tool authoring demonstrating read-only lookup (`SampleLookupTool`) and confirmed mutating order (`SampleOrderTool`) with topic allowlists (`AllowedToolIds`)
- Bounded tool catalog registration (`AddConversaCoreTools()`)
- `AI_DEVELOPER_GUIDE.md`: Practical authoring guide for domain developers and coding assistants
- No domain-agent subclass, manual topic registry, or UI event bridge

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
├── Topics/
│   └── SampleTopic/                      # Sample topic with cards
├── Pages/
│   └── Index.razor                       # Chat UI bound to IConversationRuntime
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
- Microsoft.SemanticKernel.Connectors.* (1.71.0)
- Microsoft.Data.Sqlite (9.0.10)
- Microsoft.Extensions.AI (10.2.0)
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

### Framework references
Repository builds use `ConversaCoreDev` project references so template source is always checked
against the current framework. Generated projects continue to use the DLLs in `lib/` until the
package-consumption work tracked by CC-607 replaces them. Building the main framework projects
refreshes those source-template DLLs through their post-build targets.

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
