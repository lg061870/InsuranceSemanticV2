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
  - `SampleHostOutputTopic`: Bounded host-output authoring demonstrating typed one-way notifications (`SampleHostNotification`) and correlated two-way host interactions (`SampleHostInteractionRequest` / `SampleHostInteractionResponse`)
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
└── templates/
    └── conversacore-blazor/
        └── .template.config/
            └── template.json             # Template definition
```

## Dependencies

The template project includes all required NuGet packages:
- ConversaCore (1.0.0)
- ConversaCore.UI (1.0.0)
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

### Framework references & NuGet package consumption
- **Development within repository:** Repository builds use `ConversaCoreDev=true` project references so template source is always checked against the current framework.
- **Package consumption:** Standalone template consumers consume `ConversaCore` and `ConversaCore.UI` (v1.0.0) NuGet packages directly from NuGet or a local feed (`artifacts/packages`), requiring zero copied binaries.

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
- Ensure `ConversaCore` and `ConversaCore.UI` packages are built (`dotnet pack`) or available on your package source
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

For more information, see:
- [ConversaCore Topic Authoring Guide](/docs/ConversaCore.TopicAuthoringGuide.md)
- [ConversaCore Generator Integration Contract](/docs/ConversaCore.GeneratorIntegrationContract.md) (ScriptEditor#46)

