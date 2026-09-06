# ConversaCore WP0 Current-State Inventory

- **Status:** Working baseline
- **Branch:** `codex/conversacore-runtime-foundation`
- **Covers:** CC-000, CC-001, CC-002, and CC-005
- **Related plan:** [ConversaCore.TransformationWorkBreakdown.md](ConversaCore.TransformationWorkBreakdown.md)

## 1. Purpose

This document records the framework surfaces that must be protected, migrated, or retired before the target runtime is introduced. It is a current-state inventory, not a target API specification. Items remain provisional until the characterization tests in CC-003 and CC-004 establish their observable behavior.

## 2. Executive findings

1. The domain agent is not a pass-through. `DomainAgentService` currently owns orchestration, topic selection, event wiring, activity execution, fallback behavior, and reset/cleanup concerns. `InsuranceAgentServiceV2` must inherit it and still performs domain-specific startup mutation and UI subscription.
2. Topic registration has two competing runtime paths. `TopicManager` receives scoped `IEnumerable<ITopic>`, while the singleton `TopicRegistry` is populated later by resolving scoped topic instances from a manually created startup scope.
3. The host-event contract is an untyped string plus `object` payload and mutable workflow context. The InsuranceAgent host both updates UI and performs business persistence from the same event callback.
4. Event names have already drifted. `Home.razor` consumes legacy names such as `customer_console_show`; `MarketingT2Topic` emits dotted names such as `ui.dashboard.show` and `ui.progress.update`, which the switch does not recognize.
5. The main isolation risk is concrete: a singleton registry retains scoped, stateful topic objects and their scoped conversation/workflow contexts after the startup scope is disposed.

## 3. CC-000 — Public and semi-public API inventory

| Surface | Current role and consumers | Target disposition |
|---|---|---|
| `ServiceCollectionExtensions.AddConversaCore` | Host entry point; registers AI, context, topic, document, and vector services. Called by InsuranceAgent and InsuranceLeadsAgent. | Keep as the public composition root, replace primitive options with validated framework options, and make runtime instrumentation automatic. |
| `ServiceCollectionExtensions.ConfigureTopics` | Imperatively copies resolved scoped `ITopic` instances into `TopicRegistry`. Called from startup scopes in both sample hosts. | Remove from host code. Framework should build an immutable catalog and activate topics per conversation scope. |
| `ServiceCollectionExtensions.ResetConversaCore` | Resets the singleton registry and re-registers resolved topics. | Replace with session/runtime reset; catalog metadata must not be reset. |
| `DomainAgentService` | Framework orchestration base class: selection, execution, fallback, activity event subscription, host-event bubbling, and lifecycle. Extended by `InsuranceAgentServiceV2`. | Collapse behind a concrete framework-owned facade. A domain host should register topics and options, not subclass orchestration. |
| `InsuranceAgentServiceV2` | Thin in some areas, but still chooses/mutates the start topic and exposes chat-window subscription. | Remove after facade migration; move all generic behavior into ConversaCore. |
| `TopicRegistry` | Singleton mutable collection of live `ITopic` objects. Used by startup and runtime selection. | Replace with immutable topic descriptors/catalog; do not retain scoped topic instances. |
| `ITopicManager` / `TopicManager` | Scoped lookup and semantic topic matching over resolved `ITopic` instances. | Split catalog lookup, bounded selection, and per-session activation into framework services. |
| `ITopic` / `TopicFlow` | Domain authoring/runtime topic abstraction; contains an activity queue and forwards activity events. | Keep a topic authoring abstraction, but move lifecycle/event instrumentation into the runtime. |
| `TopicFlowActivity` | Base class for executable topic steps with state, pause, and termination behavior. | Keep as an authoring/runtime extension point; expose only supported hooks and make execution framework-owned. |
| `TopicWorkflowContext` | Mutable activity/topic data bag passed widely, including to the UI host. | Make session-scoped and keep internal where possible; expose typed read-only host payloads instead of the context object. |
| `IConversationContext` / `ConversationContext` | Scoped state, topic chain, and conversation metadata shared by topics and activities. | Retain as a session-owned abstraction with explicit isolation and reset semantics. |
| `CustomEventTriggeredEventArgs` / `ICustomEventTriggeredActivity` | Untyped external hook (`EventName`, `object?`, context, wait flag) bubbled activity → topic → domain agent → UI. | Compatibility adapter only. Replace with typed output, host notification, and host interaction contracts. |
| `EventTriggerActivity` | Emits one-way events or waits for a UI response. | Keep temporarily as compatibility syntax over the typed host dispatcher. |
| `CustomChatWindowV3` | Blazor rendering/input shell; directly subscribes to domain-agent events. Used by InsuranceAgent and the SDK sample. | Bind to the runtime facade and render standard outputs; do not own orchestration. |
| `IIntegrationService` and integration activities | Executes configured external integrations such as the Zapier demo. | Separate deterministic tools from host UI events; retain adapters behind explicit tool policy. |
| `INavigationEventService` | Separate host navigation event path used by InsuranceAgent. | Classify as a host notification or host interaction; avoid a parallel event bus. |

### Boundary conclusion

The desired developer experience is not supported by the current public surface. The target should require only:

```csharp
builder.Services.AddConversaCore(options => { /* framework options */ });
builder.Services.AddConversaCoreTopicsFromAssembly(typeof(Program).Assembly);
```

Topic discovery, validation, catalog creation, per-session activation, event instrumentation, and cleanup should all happen inside the framework.

## 4. CC-001 — Topic registration inventory

### 4.1 Current registration paths

| Host | Domain registration | Runtime catalog population | Observation |
|---|---|---|---|
| InsuranceAgent | `AddInsuranceTopics()` registers 22 scoped `ITopic` factories. | `Program.cs` creates a scope and calls singleton `TopicRegistry.ConfigureTopics(scope.ServiceProvider)`. | The singleton keeps scoped topic instances after the startup scope is disposed. |
| InsuranceLeadsAgent / SDK sample | `AddConversaCoreDomainTopics()` registers 5 scoped `ITopic` factories. | `Program.cs` repeats the same manual scope and `ConfigureTopics` call. | Template teaches framework plumbing that should be internal. |
| ConversaCore | `AddConversaCore()` registers both singleton `TopicRegistry` and scoped `ITopicManager`. | `TopicManager` independently receives all scoped `ITopic` instances. | Registry and manager form duplicate sources of truth. |

### 4.2 InsuranceAgent registered topics

`ConversationStartTopic`, `BeneficiaryInfoDemoTopic`, `CaliforniaResidentTopic`, `BeneficiaryRepeatDemoTopic`, `BeneficiaryUserDrivenTopic`, `ComplianceTopic`, `ContactHealthTopic`, `ContactInfoTopic`, `CoverageIntentTopic`, `EmploymentTopic`, `DependentsTopic`, `HealthInfoTopic`, `InsuranceContextTopic`, `LeadDetailsTopic`, `LifeGoalsTopic`, `HandDownDemoTopic`, `RadioButtonDemoTopic`, `NewbieTopic`, `MarketingT1Topic`, `SemanticActivitiesDemoTopic`, `EventTriggerDemoTopic`, and `ZapierIntegrationDemoTopic`.

### 4.3 Registration/reference defects

| Finding | Evidence | Required decision |
|---|---|---|
| T2 exists but is not registered. | `MarketingTypeTopics/MarketingT2Topic.cs` exists; `AddInsuranceTopics()` registers only T1. | Register or retire after determining whether T2 is a supported insurance path. |
| T2 class and runtime names differ. | Class is `MarketingT2Topic`; base name is `MarketingTypeTwoTopic`. | Adopt a stable topic ID independent of class/display name. |
| T3 is referenced conceptually but has no implementation. | Current architecture analysis and routing history mention a T3 path; no T3 topic file is present. | Remove dead references or add an explicitly scoped backlog item. |
| Start behavior is host/service-specific. | `ConversationStartTopic` is registered as a normal domain topic and later selected/mutated by the agent service. | Move start/fallback conventions into framework options and validation. |
| Manual logger factories are repeated. | Both host registration extensions add per-topic loggers and call constructors manually. | Use ordinary DI activation and framework-provided authoring dependencies. |
| Topic names are string routing keys. | `TriggerTopicActivity` and topic-chain calls use string names. | Introduce validated stable IDs and fail startup on unresolved references. |

### 4.4 Catalog rules to characterize before replacement

- Case-insensitive name lookup and duplicate suppression.
- Semantic matching order and score thresholds.
- Start and fallback topic selection.
- Subtopic hand-down, return, and topic-chain mutation.
- Reset and terminated-topic behavior.
- Whether constructors or initialization mutate shared context.

## 5. CC-002 — InsuranceAgent host-event inventory

### 5.1 Transport

`EventTriggerActivity` creates `CustomEventTriggeredEventArgs`; composite/conditional activities and `TopicFlow` bubble it to `DomainAgentService`; `CustomChatWindowV3` and `Home.razor` subscribe. Payloads may be anonymous objects, dictionaries, JSON strings, `JsonElement`, `UiProgressEvent`, or arbitrary POCOs. `Home.razor` therefore contains a reflection/JSON normalization layer.

### 5.2 Events consumed by `Home.razor`

| Event | Host behavior | Current shape | Target classification |
|---|---|---|---|
| `customer_console_show` | Opens the customer console and forwards subsequent events to it. | One-way, untyped. | Typed host notification (`ShowPanel`). |
| `lead_details_submitted` | Updates progress, creates the lead, saves contact information, and stores returned lead ID in component state. | One-way event causing business writes. | Deterministic tool for persistence plus a separate progress output/notification. |
| `life_goals_submitted` | Updates progress and persists life-goal data. | One-way event causing business write. | Deterministic tool plus progress output. |
| `coverage_intent_submitted` | Updates progress and persists coverage intent. | One-way event causing business write. | Deterministic tool plus progress output. |
| `health_info_submitted` | Updates progress and persists health information. | One-way event causing business write. | Deterministic tool plus progress output. |
| `dependents_submitted` | Updates progress and persists dependents. | One-way event causing business write. | Deterministic tool plus progress output. |
| `employment_submitted` | Updates progress and persists employment. | One-way event causing business write. | Deterministic tool plus progress output. |
| `beneficiaries_submitted` | Updates progress and persists beneficiaries. | One-way event causing business write. | Deterministic tool plus progress output. |
| `contact_info_submitted` | Updates progress; persistence call is commented out. | One-way, partially implemented. | Decide whether persistence belongs in the lead tool or a dedicated tool. |
| `qualification_complete` | Marks progress complete and updates UI. | One-way, untyped. | Standard runtime progress/completion output. |

All events are also forwarded to `CustomerConsole.UpdateFromChatEvent` after its panel is visible, creating a second string-based consumer contract.

### 5.3 Events emitted by `MarketingT2Topic`

| Event | Intent | Current consumer status | Target classification |
|---|---|---|---|
| `ui.dashboard.show` | Show the customer console for the T2 path. | Not recognized by the `Home.razor` switch; still forwarded only if the console is already visible. | Typed host notification. |
| `ui.progress.update` | Report 50% progress. | Not recognized by the switch. | Standard progress output. |
| `ui.progress.complete` | Report completion. | Not recognized by the switch. | Standard completion output. |

This drift demonstrates why event IDs and payload schemas must be validated at startup and represented by framework contracts.

### 5.4 Interaction path

`EventTriggerActivity(waitForResponse: true)` is the current request/response hook. The activity exposes waiting information and expects the UI to call its response handler. Characterization tests currently fail around this lifecycle, so the compatibility behavior must be locked before migration. The target equivalent is a typed `HostInteractionRequest<TPayload, TResponse>` with correlation, cancellation, timeout, and exactly-once completion.

## 6. CC-005 — Cleanup candidate register

| Candidate | Classification | Reason / prerequisite |
|---|---|---|
| `InsuranceAgentService` | Delete-later | Legacy standalone orchestration duplicates framework `DomainAgentService`; retain until characterization tests cover its required behavior. |
| `InsuranceAgentServiceV2` | Migrate then delete | Current active domain subclass; replace with the framework facade. |
| `HybridChatService` and `HybridChatServiceV2` | Review / archive | Experimental parallel chat paths; establish whether either is referenced by active pages. |
| `ConversaCore/NEWCODE/*V2` | Archive or delete-later | Unintegrated alternate topic/activity model competes with the active abstractions. |
| `TopicRegistry.ConfigureTopics` startup blocks | Delete after catalog activation | Host-owned instrumentation and lifetime violation. |
| `TopicRegistry` live-instance storage | Replace | Conflicts with session isolation. |
| `CustomChatWindowV3` version suffix | Rename after migration | It is the active UI component, not merely an experiment; preserve behavior first. |
| Demo topics (`*DemoTopic`) | Retain selectively as samples | Move supported examples into clearly named sample projects; remove dead experiments only after coverage. |
| `ZapierIntegrationDemoTopic` / integration activity | Migrate to tool sample | It models an external operation more closely than a UI event. |
| Console/debug logging in framework execution classes | Replace | Use structured `ILogger` and remove verbose event-bubbling diagnostics after tests exist. |
| Generated `*.visual-check.*` files | Ignore / regenerate | Validation artifacts are reproducible and now ignored. |
| Legacy event aliases | Compatibility window | Map old names to typed contracts temporarily; warn and remove on a documented schedule. |

## 7. Safety-net status

### CC-003 characterization coverage

Existing tests cover portions of activities, events, topic flow, vector storage, and services, but they do not yet constitute the migration safety net. Required additions remain:

- conversation start and fallback interruption;
- active-topic input and required-card behavior;
- subtopic hand-down and return;
- reset and async semantic completion;
- each current InsuranceAgent host notification and persistence trigger;
- event alias/drift behavior.

### CC-004 isolation coverage

`ConversaCore.Tests/Characterization/LegacySessionIsolationTests.cs` now supplies four executable scenarios using the actual `AddConversaCore` registrations and a disposable probe topic:

- Direct scoped topic resolution isolates topic instances and both contexts.
- The startup-populated singleton registry retains a disposed topic and exposes its mutable workflow state to both sessions.
- Reconfiguring from a later scope silently retains the original topic with the same name.
- Resetting from one scope changes the registry seen by another scope.

These are legacy characterization tests: passing means the documented defect is reproduced. Keep them on the compatibility path and add positive isolation tests for the replacement catalog/runtime before marking its isolation gate complete.

`ConversaCore.Tests/Characterization/LegacyHostInteractionTests.cs` adds three scenarios: deferred notification payload resolution with exactly one completion, an inline host response dropped before the waiting transition, and cancellation leaving stale waiting markers. They use explicit cancellation, no AI calls, and no timeout sleeps.

The targeted characterization run now passes 17 tests. `LegacyOrchestrationTests.cs` exercises the real domain agent with deterministic routing probes: start still requires a domain override, all topics compete on each message, matched input is not passed to `ProcessMessageAsync`, fallback receives the unmatched prompt, no fallback emits a missing-topic notification, and async follow-up is inserted and forwarded without executing immediately.

`LegacyWorkflowTests.cs` covers real flow reset, subtopic wait and explicit return, conversation reset, and the hidden required-card flag. A required `AdaptiveCardActivity<T>` reads as optional through `TopicFlowActivity` because its property hides rather than overrides the base property. Flow reset clears authored activities; a rerun requires rebuilding them. A subtopic trigger is marked completed while the parent remains waiting.

CC-003 remains partial: full card submission/validation, fallback interruption and resumption through the real callbacks, domain start/compliance composition, actual asynchronous semantic completion, and exhaustive insurance notification coverage are still outstanding. The probe tests isolate routing entry points; they do not certify end-to-end domain behavior.

Progress and validation are recorded on [CC-003 #17](https://github.com/lg061870/InsuranceSemanticV2/issues/17) and [CC-004 #18](https://github.com/lg061870/InsuranceSemanticV2/issues/18).

### Baseline validation on this branch

- NuGet restore: passes.
- Vulnerability audit (`--vulnerable --include-transitive`): no known vulnerable packages reported by configured sources.
- Solution build: passes with 0 errors and 22 warnings.
- `ConversaCore.Tests`: 40 passed, 9 failed, 1 skipped. Five failures are in SQLite vector-store tests with a `SqliteVec` type-load incompatibility; one collection assertion fails; three one-minute failures are in the event wait/response lifecycle. These failures are baseline work items and must not be hidden during the migration.
- `InsuranceSemanticV2.IntegrationTests`: 33 failed before test execution because the test host does not provide `JWT:SecretKey`; this is a test-fixture configuration defect rather than a compile failure.

## 8. Immediate WP0 next steps

1. Use the CC-004 legacy tests as evidence for a descriptor-only catalog; add positive session-isolation tests alongside the replacement runtime.
2. Add focused CC-003 tests around start, fallback, required card, hand-down/return, and event wait/response without using real AI services.
3. Decide whether the SQLite connector remains in framework core, moves behind an optional package, or migrates to the maintained Community Toolkit provider.
4. Approve stable topic IDs and typed host-event names before public-contract implementation begins.
5. Convert these findings into ADR decisions under CC-006.
