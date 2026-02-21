## ConversaCore VS Template & Structure

This spec defines the Visual Studio project template and recommended structure for building self-hosted domain applications on the ConversaCore SDK.

### 1. Goals

- Provide a "ConversaCore Blazor Server" project template that:
  - Self-hosts a domain agent in the same web project.
  - Wires ConversaCore and ConversaCore.UI with minimal boilerplate.
  - Gives domain developers a clear, consistent folder layout for topics, cards, models, and documents.
- Keep ConversaCore itself closed/immutable; customization happens only in the domain project.

### 2. SDK Components

The ConversaCore SDK, from the domain developer’s perspective, consists of:

- **Core libraries**
  - `ConversaCore` – topics, activities, state machines, intent recognition, vector DB, Semantic Kernel integration.
  - `ConversaCore.UI` – chat window components (e.g., `CustomChatWindowV3`), adaptive card renderer, UI models.
- **VS Project Template**
  - "ConversaCore Blazor Server" template based on the `SimpleBlazorDemo` wiring pattern.
- **VS Add-ins (separate specs)**
  - Topic Tool (topic/card/model authoring).
  - Document Embedder Utility (document collections and ingestion).

### 3. Project Template Behavior

When a developer creates a project using the "ConversaCore Blazor Server" template:

1. **Project type**
   - Blazor Server app targeting the current supported .NET version.
   - References `ConversaCore` and `ConversaCore.UI` NuGet packages or projects.

2. **Startup wiring (Program.cs)**
   - Registers standard ASP.NET/Blazor services.
   - Calls `AddConversaCore(openAIApiKey, embeddingModel)` with a placeholder for `OPENAI_API_KEY` (environment variable or appsettings binding).
   - Registers:
     - `IChatInteropService` implementation from `ConversaCore.UI`.
     - A domain-specific `DomainAgentService` subclass (e.g., `MyDomainAgentService`).
     - System topics (ConversationStart, Fallback, OnError, etc.).
     - One or more sample domain topics.
   - After building the app, creates a DI scope and calls `TopicRegistry.ConfigureTopics(scope.ServiceProvider)`.
   - Optionally calls a domain document-ingestion coordinator at startup (commented by default).

3. **UI Integration**
   - Includes `_Imports.razor` with `ConversaCore.Agentic`, `ConversaCore.UI.Components`, and `ConversaCore.UI.Models` namespaces.
   - Injects the domain-specific agent service into a default page or layout.
   - Drops the `CustomChatWindowV3` component with:
     - `AgentService` bound to the injected domain agent.
     - `Style` set to `ChatStyle.ChatWindow` or `ChatStyle.SidebarChat`.
     - `SubscribeToEvents` wired to the agent’s subscription method.

### 4. Standard Folder Structure

At the project root (domain app):

- `Topics/`
  - `<TopicName>/`
    - `<TopicName>.Domain.cs` – developer-authored partial topic (narrative, metadata).
    - `<TopicName>.Generated.cs` – tool-generated partial topic (`TopicFlow` implementation).
    - `Cards/` – topic-specific card builders.
    - `Models/` – topic-specific models.
    - `Documents/` – topic-scoped documents for embedding (optional).
- `Cards/`
  - `Shared/` – shared card builders used by multiple topics.
- `Documents/`
  - Global/shared documents for the domain knowledge base.
- `wwwroot/`
  - `adaptivecards/`
    - `shared/` – shared raw JSON card definitions.
    - `<topicname>/` – topic-specific raw JSON card payloads.

This structure is the baseline for the Topic Tool and Document Embedder to operate on.

### 5. Domain Agent Pattern

The template includes a domain-specific agent service:

- `Services/MyDomainAgentService.cs`
  - Inherits `DomainAgentService` from `ConversaCore.Agentic`.
  - Injects `TopicRegistry`, `IConversationContext`, `TopicWorkflowContext`, and logger.
  - Overrides `OnUserMessageReceivedAsync` (and other virtual hooks as needed) to:
    - Capture relevant context (e.g., last user message) in `TopicWorkflowContext`.
    - Short-circuit to resume active `TopicFlow` instances that are `WaitingForInput`.
  - Exposes `SubscribeToChatWindowEvents(CustomChatWindowV3 chatWindow)` to wire UI events (conversation start, user messages, card submissions, reset) to the base agent methods.

Program.cs registers this service both as the concrete type and as `DomainAgentService` for flexibility.

### 6. Sample Topics and Cards

The template ships with a minimal set of sample topics and cards, illustrating patterns without prescribing domain content:

- One simple TopicFlow demonstrating:
  - A welcome `SimpleActivity`.
  - A short `DelayActivity`.
  - A single `AdaptiveCardActivity<TCard, TModel>` for basic input.
  - A `CanHandleAsync` that prefers a simple test phrase.
- Optional second topic showing hand-down via `TriggerTopicActivity`.

Cards and models:

- A simple contact/lead-capture card (builder + model) under `Topics/SampleTopic/Cards` and `Topics/SampleTopic/Models`.
- Shared card examples (e.g., consent) under `Cards/Shared`.

### 7. Configuration and Environment

The template includes:

- `appsettings.json` and `appsettings.Development.json` with placeholders for:
  - ConversaCore/LLM configuration (model names, endpoints if needed).
  - Optional integration settings (e.g., webhooks, messaging providers).
- README section explaining:
  - How to set `OPENAI_API_KEY` (environment variable or secrets).
  - How to adjust the embedding model name.

### 8. Extensibility and Tools Alignment

- The VS Topic Tool and Document Embedder operate against this structure:
  - They create topic subfolders, partial classes, cards, models, and documents according to the layout above.
  - They maintain DI registrations in Program.cs (or a designated registration class) without touching ConversaCore.
- Future templates may extend this baseline (e.g., multiple projects or separate agent host), but this spec defines the initial self-hosted Blazor Server template.
