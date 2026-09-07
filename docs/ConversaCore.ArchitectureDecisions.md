# ConversaCore transformation decisions

Tracking: [CC-006 #20](https://github.com/lg061870/InsuranceSemanticV2/issues/20).

These decisions formalize the target architecture already agreed in the project discussion and authorized for implementation. They are architecture decisions, not claims that the implementation or release gates are complete. The detailed source is [Target Architecture](ConversaCore.TargetArchitecture.md); execution remains governed by the GitHub work breakdown.

## ADR-001 — Framework-owned facade

Decision: ConversaCore provides `IConversationRuntime` with awaitable start, message, card submission, host response and reset commands. Domain developers register topics and optional tools; no domain agent subclass is required. ConversaCore.UI consumes this public contract. Core cannot reference UI or a domain application.

Reason: the legacy agent currently requires a start override, owns generic routing and subscriptions, and mixes insurance reset conventions into framework code. The facade moves instrumentation into the framework while topics retain control over business sequence and scope.

Consequence: migrate with an isolated compatibility adapter; remove subclasses only after InsuranceAgent and SDK parity checks. Tasks: CC-200, CC-306, CC-501, CC-502, CC-701.

## ADR-002 — Immutable catalog and scoped activation

Decision: singleton catalogs retain immutable descriptors and activation metadata only. A conversation-scoped activator creates fresh mutable topic executions within that conversation. Validate stable IDs, aliases, references, and start/fallback declarations without resolving and retaining live scoped topics at startup. Await initialization before routing or execution.

Reason: CC-004 reproduces retained disposed topics, shared workflow state, and cross-session reset through the legacy singleton registry. Ordinary scoped resolution itself is isolated.

Consequence: do not infer lifetime safety from a scoped registration alone. Positive concurrency tests must cover activation, reset, cancellation and disposal. Tasks: CC-101 through CC-105, CC-201 through CC-203, CC-209, CC-212.

## ADR-003 — Ordered asynchronous output

Decision: the runtime emits typed `ConversationOutput` values through asynchronous disposable subscriptions, ordered per conversation. The runner owns propagation; domain code does not manually hook every activity. Subscriber failure is isolated, and cancellation/disposal ends pending work predictably. UI receives immutable payloads or snapshots.

Reason: event chains currently spread orchestration across framework, domain service and UI. `async void` paths hide completion and failure from callers.

Consequence: concrete queue implementation and buffer limits are implementation choices under CC-301, not authorization for unbounded memory or untracked tasks. Backpressure/disconnect behavior must be specified and tested there. Tasks: CC-210, CC-300 through CC-302, CC-306 through CC-309.

## ADR-004 — Distinguish notification from interaction

Decision: domain websites react through typed host notifications or correlated host interaction requests. Notifications do not wait for a user response. Interactions register their pending correlation before dispatch and resolve exactly once with a validated response, cancellation or timeout. Late and duplicate responses have defined rejection behavior. Standard chat questions use cards/prompts where sufficient.

Reason: characterization proves inline responses are dropped before the current waiting transition and cancellation leaves stale markers. String names already drift between producers and consumers.

Consequence: preserve old syntax in a temporary `EventTriggerActivity` adapter; never hand the host a mutable workflow context. Tasks: CC-303 through CC-305, CC-307, CC-310, CC-503, CC-506.

## ADR-005 — Typed tools return domain results

Decision: tools implement `IConversaTool<TRequest,TResult>`; they are neither topics nor activity subclasses. A framework `InvokeToolActivity` invokes a declared tool and returns typed data to the topic. Domain persistence and external operations execute through tools, independent of whether an optional site panel is mounted. `IIntegrationService` remains transport behind tools.

Reason: current lead persistence depends on a Blazor event subscriber and its local lead ID, so business progress depends on presentation state. Reusable capabilities need explicit results and failures.

Consequence: enforce validation, trusted identity, authorization, declared confirmation, timeout and mutation idempotency at execution. Expose a narrow execution context; do not make a catalog or context an unrestricted service locator. Tasks: CC-400 through CC-405, CC-408 through CC-412, CC-504, CC-505, CC-508.

## ADR-006 — Explicit bounded semantic selection

Decision: topic routing precedes tool selection. Only an explicitly authored selection activity can choose among that topic's allowlisted eligible tools. Cache metadata/schemas, prefilter deterministically and pass a bounded top-K set to the model. The model proposes a selection; framework policy still authorizes and validates it.

Reason: ConversaCore guides business conversations through authored topics. An always-on global tool planner would change that product boundary and introduce unnecessary model work on ordinary messages.

Consequence: ranking strategy and measured top-K/performance budgets are implementation details under CC-407/CC-804. No global discovery loop is introduced. Tasks: CC-405 through CC-409, CC-411, CC-412, CC-804.

## Decisions still owned by implementation tasks

Buffer capacities and disconnect replay (CC-301/309), exact public record shapes and naming (WP1), provider/package remediation (CC-708/800), and numerical performance budgets (CC-804) remain to be specified with evidence. None changes the six architecture boundaries above. Any proposal that does change a boundary must update this decision record and its issue before implementation.

## Verification boundary

Legacy characterization tests are a safety baseline, not proof of target compliance. Existing vector-store, event-wait and integration-fixture failures remain recorded until repaired. G0 through G6 retain their separate acceptance criteria; accepting these decisions does not waive any gate.
