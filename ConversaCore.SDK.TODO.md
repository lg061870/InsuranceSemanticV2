# ConversaCore SDK TODO

High-level roadmap for the ConversaCore SDK work, independent of insurance-specific domain code. All work should live in new SDK-focused projects/solution folders.

---

## A. ConversaCore VS Template (conversacore-vs-template-spec.md)

- [x] Create SDK solution folder
  - [x] Add `ConversaCore.SDK/` solution folder to contain SDK-only projects.

- [x] Blazor template host project
  - [x] Add new Blazor Server project `ConversaCore.BlazorTemplateHost` under `ConversaCore.SDK`.
  - [x] Copy **generic** wiring from `SimpleBlazorDemo` (Program.cs, _Imports, chat window placement), stripping all insurance-specific topics/services.
  - [x] Reference only `ConversaCore` and `ConversaCore.UI` (no InsuranceAgent/Insurance* projects).

- [x] Domain agent baseline
  - [x] Implement `Services/MyDomainAgentService.cs` inheriting `DomainAgentService`.
  - [x] Wire `SubscribeToChatWindowEvents` to `CustomChatWindowV3` events.
  - [x] Register it in Program.cs as both `DomainAgentService` and its concrete type.

- [x] Sample topics and cards
  - [x] Create `Topics/SampleTopic/` with one simple `TopicFlow` demonstrating:
    - [x] `SimpleActivity` → `DelayActivity` → `AdaptiveCardActivity<TCard, TModel>` chain.
    - [x] Minimal `CanHandleAsync` based on a test phrase.
  - [x] Add sample card + model under `Topics/SampleTopic/Cards` and `Topics/SampleTopic/Models`.
  - [ ] Optionally add a second sample topic that uses `TriggerTopicActivity` for hand-down.

- [x] Standard folder layout
  - [x] Ensure template host project uses:
    - [x] `Topics/<TopicName>/Cards/Models/Documents`.
    - [x] `Cards/Shared/`.
    - [x] `Documents/`.
    - [x] `wwwroot/adaptivecards/shared` and `wwwroot/adaptivecards/<topicname>`.

- [ ] Template packaging
  - [x] Add `.vstemplate` definition for `ConversaCore.BlazorTemplateHost`.
  - [x] Set basic template metadata (name, description, project type, default name, icon placeholder).
  - [ ] Verify creating a new project from the template builds and runs without any insurance-specific code (manual step in VS).

---

## B. ConversaCore Topic Tool VSIX (plan-topicTool.prompt.md)

- [ ] VSIX scaffolding
  - [x] Add `ConversaCore.SDK/ConversaCore.TopicTool.VSIX` project.
  - [x] Implement basic VS extension entrypoint, menu command, and tool window.

- [ ] Topic spec and storage
  - [x] Define JSON schema for topic specs (name, namespace, intent, priority, narrative, cards, subtopics, flags).
  - [x] Implement read/write helpers to store specs under `Topics/<TopicName>/`.

- [ ] Partial class pattern
  - [x] Implement generation of:
    - [x] `Topics/<TopicName>/<TopicName>.Domain.cs` (developer-facing partial).
    - [x] `Topics/<TopicName>/<TopicName>.Generated.cs` (tool-owned partial).
  - [x] Ensure regeneration overwrites only `.Generated.cs`, leaving `.Domain.cs` intact.

- [ ] Embedded authoring guide
  - [x] Create a `ConversaCore Topic Authoring Guide` document describing:
    - [x] TopicFlow lifecycle and hand-down/regain control.
    - [x] Allowed activity types + examples.
    - [x] Hard constraints (no new activity types / no framework edits).
  - [x] Embed guide as a VSIX resource and version it.
  - [x] Ensure all AI calls include this guide in the system prompt.

- [ ] New Topic / Edit Topic UX
  - [x] Implement "New Topic" wizard:
    - [x] Collect topic name, namespace, intent, keywords, priority, flow narrative, card usage, subtopics.
    - [x] Create folder structure, domain partial, spec file.
    - [x] Trigger generated partial creation (via TopicCodeGenerator; AI backend pluggable via ITopicAiGenerator).
  - [x] Implement "Edit Topic" tool window:
    - [x] Load existing topic spec + domain partial metadata (spec + partial-pattern guard).
    - [x] Allow edits and regenerate `.Generated.cs` (Regenerate code button).

- [ ] DI registration integration
  - [x] Implement logic to locate the host project’s Program.cs or a dedicated `TopicRegistration` class.
  - [x] Maintain a guarded region where the tool adds/removes scoped `ITopic` registrations.
  - [x] Add validation that `TopicRegistry.ConfigureTopics` sees the registered topics (structural check for AddScoped<ITopic> in guarded region).

- [ ] Safety + analysis
  - [x] Mark generated files with AUTO-GENERATED headers including tool + guide version.
  - [x] Add a post-generation validator (Roslyn analyzer or simple checks) to flag disallowed APIs/namespaces in generated topics.

- [ ] Optional: topic simulation
  - [x] Add a small runtime harness project (SDK-only) to execute a single TopicFlow in isolation.
  - [x] Integrate with VSIX UI to send test messages and display emitted messages/cards.

---

## C. Document Embedder Utility (plan-documentEmbedderUtility.prompt.md)

- [ ] Integrate Document Embedder into Topic Tool VSIX (or separate VSIX)
  - [x] Decide whether it lives in `ConversaCore.TopicTool.VSIX` or a sibling `ConversaCore.DocumentEmbedder.VSIX` (chosen: `ConversaCore.TopicTool.VSIX`).

- [ ] Collection modeling and config
  - [x] Define a project-level config file (e.g., `.conversacore.documents.json`) describing:
    - [x] Collection name.
    - [x] Scope: Global vs Topic-specific.
    - [x] Source folders under `Documents/` tree and file patterns.
    - [x] Options (chunking, max tokens, languages).
  - [x] Implement helpers to map topic IDs + collection names → vector DB collection names.

- [ ] Document Collections tool window
  - [x] Build UI to create/edit/delete collections.
  - [x] Validate paths existence and duplicate names; surface warnings.

- [ ] Ingestion service codegen
  - [x] Generate per-collection or grouped ingestion services in the host project (e.g., `<Name>DocumentsEmbeddingService`), following the `LifeInsuranceBasicsEmbeddingService` pattern but generic.
  - [x] Generate a `DocumentIngestionCoordinator` that can run:
    - [x] Single collection.
    - [x] All collections for a topic.
    - [x] All global collections.

- [ ] Startup wiring
  - [x] Maintain a controlled registration region in Program.cs or a `DocumentIngestionRegistration` helper.
  - [x] Register all generated ingestion services + coordinator.
  - [x] Bind the document-collection config class(es) (design-time via .conversacore.documents.json; generated services bake settings at build time).
  - [x] Add a commented sample call to run ingestion at startup.

- [ ] Design-time ingestion triggers
  - [x] Add VS commands for:
    - [x] "Dry run" ingestion (list candidate files + target collections).
    - [x] "Run ingestion" (document how to execute coordinator for selected/all collections from the host app).
  - [x] Implement a helper entry point pattern via the generated `DocumentIngestionCoordinator` and commented sample in Program.cs.
  - [x] Wire VSIX to trigger dry-run analysis and surface execution guidance/results in the tool window.

- [ ] Naming & mapping conventions
  - [x] Finalize vector collection naming scheme (e.g., `<AppPrefix>_<TopicOrGlobal>_<CollectionName>`).
  - [x] Ensure all generated services use the same scheme consistently via `VectorCollectionNameHelper`.
