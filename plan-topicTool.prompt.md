## Plan: ConversaCore Topic Tool (VS Add‑in)

Design a Visual Studio add‑in (“ConversaCore Topic Tool”) that treats ConversaCore as a closed SDK and gives the domain developer a constrained, AI-assisted way to define topics, cards, models, and configuration. The tool generates partial topic classes and related assets using only the approved activity types and patterns, with an embedded, non-editable reference document that drives the AI. It also maintains DI registration and basic topic metadata.

### 1. Topic authoring model and partial‑class split

1. Every topic uses a standard folder structure, for example:
   - `Topics/SomeTopic/SomeTopic.Domain.cs` (developer‑facing partial)
   - `Topics/SomeTopic/SomeTopic.Generated.cs` (AI‑generated partial)
   - `Topics/SomeTopic/Cards/...`
   - `Topics/SomeTopic/Models/...`
2. In the domain partial, the developer writes only natural‑language description and high-level settings:
   - Intent keywords / descriptions
   - Priority
   - Narrative of the flow (“First welcome, then ask name, then...”) and any constraints
3. In the generated partial, the AI writes the `TopicFlow` implementation:
   - Constructor and `BuildWorkflow()` using only allowed activities
   - `CanHandleAsync` implementation based on intent description/keywords
   - Any internal helper methods required to glue activities together
4. The tool keeps a machine-readable “topic spec” (e.g., JSON) in the topic folder as the source of truth for regeneration without losing developer intent.

### 2. Embedded ConversaCore authoring guide for AI

1. The add‑in embeds a non-editable “ConversaCore Topic Authoring Guide” resource that:
   - Explains TopicFlow, lifecycle, and hand‑down/regain control
   - Enumerates each allowed activity type with a short description and example usage
   - States hard constraints: no new activity types, no changes to ConversaCore APIs
2. The add‑in always passes this document as a fixed system prompt to its AI calls so generation respects SDK boundaries.
3. The guide is versioned, and generated files are tagged with the authoring guide version; this allows future upgrades and compatibility checks.

### 3. New Topic and Edit Topic UX in VS

1. **New Topic Wizard** collects:
   - Topic name and namespace (default folder under `Topics/<TopicName>`)
   - Intent description and optional keyword list
   - Priority
   - High-level flow description
   - Whether the topic uses cards and/or calls other topics (hand‑down)
2. On completion, the tool:
   - Creates the topic folder structure
   - Writes the developer partial with narrative, metadata, and a stable topic ID
   - Creates/updates the JSON topic spec
   - Calls AI to generate or refresh the generated partial
3. **Edit Topic Pane** lets the dev:
   - Modify the narrative, keywords, priority, and references to cards/subtopics
   - Save changes back into the JSON spec and regenerate the generated partial
4. Developer partials are never modified by the tool once created (except for controlled metadata regions, if needed).

### 4. Cards and models integrated with topic authoring

1. **Card Designer Wizard** (invoked from topic wizard or separately):
   - Choose scope: topic-specific (`Topics/<TopicName>/Cards`) or shared (`Cards/Shared`)
   - Define card purpose, fields (label, type, required, hints), and basic validation rules
   - Generate:
     - A model class in `Models` (e.g., `Topics/<TopicName>/Models/<CardName>Model.cs`)
     - A card builder class (e.g., `Topics/<TopicName>/Cards/<CardName>.cs`) that follows the approved AdaptiveCardActivity pattern
2. The topic designer shows available cards/models and lets the dev bind them into steps; the JSON spec records which cards belong to which steps.
3. On regeneration, the AI uses the spec to insert appropriate `AdaptiveCardActivity<TCard, TModel>` stages into `BuildWorkflow()`.

### 5. Topic configuration and DI registration

1. The tool surfaces topic-level configuration:
   - Intent recognition rules (keywords, sample utterances, descriptions)
   - Priority and flags (system topic vs domain topic, etc.)
   - Discoverability (selectable via free text vs TriggerTopicActivity only)
2. DI registration support:
   - The add‑in locates `Program.cs` (or a dedicated registration class) and maintains a structured region for topic registrations.
   - When a topic is added/removed, the tool adds/removes the corresponding scoped `ITopic` registration following the existing pattern.
   - Optionally centralize registrations into a `TopicsRegistration` helper class to reduce `Program.cs` churn.
3. The tool validates that `TopicRegistry.ConfigureTopics` sees the new topics and warns if registration looks incomplete.

### 6. Regeneration, safety, and constraints

1. Generated files are clearly marked as tool‑owned:
   - Header comments indicating AUTO‑GENERATED and referencing the tool + guide version
2. Regeneration process:
   - Read JSON spec + developer partial
   - Re-run AI with the embedded guide
   - Overwrite only the generated partial, leaving the developer partial intact
3. Provide a preview mode:
   - Show a structured summary/diff of topic steps before writing changes
4. Enforce allowed APIs:
   - Post-generation analyzer flags disallowed namespaces, activity base classes, or direct calls into forbidden layers

### 7. Optional extensions

1. **Topic simulation panel** inside the add‑in:
   - Instantiate a topic in isolation and simulate user messages
   - Show emitted activity sequence and messages/cards without running the whole app
2. **Multi-project awareness**:
   - Support topics in a separate class library (e.g., `MyDomain.Topics`) while the web host lives in another project
   - Let the user pick the “topic project” and the “host project” in the add‑in settings
3. **SDK/guide version upgrades**:
   - Offer an "Upgrade topic to SDK vX" flow that reinterprets the JSON spec with new activity patterns while preserving the domain narrative.
