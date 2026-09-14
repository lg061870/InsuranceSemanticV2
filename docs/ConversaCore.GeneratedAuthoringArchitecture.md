# ConversaCore generated-C# authoring architecture

**Status:** Accepted design; framework authoring contracts implemented<br>
**Tracking:** [Architecture amendment #115](https://github.com/lg061870/InsuranceSemanticV2/issues/115), [CC-900 #116](https://github.com/lg061870/InsuranceSemanticV2/issues/116), [CC-901 #117](https://github.com/lg061870/InsuranceSemanticV2/issues/117), [CC-902 #118](https://github.com/lg061870/InsuranceSemanticV2/issues/118), [CC-903 #119](https://github.com/lg061870/InsuranceSemanticV2/issues/119)<br>
**Cross-repository consumer:** [ScriptEditor#46](https://github.com/lg061870/ScriptEditor/issues/46)

## 1. Product boundary

The supported visual-authoring pipeline is:

```text
JSON diagram
    -> generated C# topic, model, and definition source
    -> Roslyn compilation
    -> ordinary ConversaCore topic registration and scoped activation
```

ConversaCore owns the public contracts generated C# targets. ScriptEditor owns diagram
schema interpretation, Roslyn syntax generation, generated assembly loading, and
round-trip editing. ConversaCore does not read ScriptEditor documents and this amendment
does not introduce a runtime JSON workflow interpreter.

Generated source is application source. It may use constructor injection and generic type
arguments exactly like hand-authored C#. Framework services therefore do not need literal
JSON representations. They do need a stable, explicit authoring surface so generators do
not duplicate framework lifecycle rules or depend unnecessarily on volatile constructor
details.

## 2. Decisions

### 2.1 Post-construction composition

Generated and newly migrated topics derive from an opt-in `ComposedTopicFlow`. The base
implements `IAsyncInitializable`; `TopicActivator` therefore composes the workflow only
after DI has completely constructed the derived object. It never invokes an overridable
member from a constructor.

The conceptual contract is:

```csharp
public sealed class MainConversation : ComposedTopicFlow
{
    private readonly IWorkflowActivityFactory _activities;

    public MainConversation(
        TopicWorkflowContext context,
        ILogger<MainConversation> logger,
        IWorkflowActivityFactory activities)
        : base(context, logger, "main") => _activities = activities;

    protected override void ComposeWorkflow()
    {
        Add(_activities.CreatePrompt(new(
            "welcome",
            "Answer accurately and concisely.")));
    }
}
```

Composition is synchronous because it builds an in-memory activity graph. Activities may
perform asynchronous work only when the runner executes them. A topic that must load data
before composition can continue to implement its own asynchronous initialization or use a
future explicitly asynchronous composed-topic contract; it must not block or launch
background work from a constructor.

`ComposedTopicFlow` guarantees:

- idempotent initialization per topic instance;
- serialized concurrent initialization;
- cleanup of a partially composed graph when composition throws;
- retry after failed or canceled initialization;
- direct `RunAsync` safety for hosts that instantiate a topic outside `TopicActivator`;
- central clear-and-recompose behavior on reset;
- no change to legacy `TopicFlow` constructor-managed topics.

The opt-in base is a compatibility step, not authorization for two permanent orchestration
runtimes. WP7 decides when migrated consumers permit the composed lifecycle to become the
single documented path and legacy constructor-managed composition to retire.

CC-901 delivered this contract in `ComposedTopicFlow`. Focused tests prove activation occurs
after derived construction, repeated and concurrent initialization composes once, failed
composition clears the partial graph and can be retried, pre-cancellation does no work,
direct execution initializes safely, reset rebuilds once, and terminated topics reject
composition.

### 2.2 Explicit activity authoring factory

Generated topics receive a scoped `IWorkflowActivityFactory` through ordinary constructor
injection. The factory exposes only supported activity construction methods and resolves
its own narrow dependencies (`Kernel`, `TopicWorkflowContext`, and `ILoggerFactory`) from
that conversation scope.

The initial factory surface covers constructor shapes that currently force generated code
to know framework plumbing:

- `CreatePrompt(PromptActivityDefinition)`;
- `CreateQuickAnswer(QuickAnswerActivityDefinition)`;
- `CreateAdaptiveCard<TModel>(GeneratedAdaptiveCardDefinition)`.

Literal-safe activities such as `SimpleActivity`, `EndActivity`, `DelayActivity`, and
`TriggerTopicActivity` remain valid direct constructor targets. ScriptEditor may emit
their ordinary constructors. The factory is not a global activity catalog, does not
perform semantic discovery, and accepts no arbitrary type name.

Definitions are immutable, validate required text and numerical ranges at construction,
and contain configuration only. They never retain scoped services. The factory itself is
scoped and may return fresh mutable activity instances only.

Rejected alternatives:

- `AsyncLocal<IServiceProvider>`;
- a context-carried service locator;
- static mutable ambient services;
- reflection-based arbitrary constructor invocation;
- asking a model to select an unrestricted activity type.

CC-902 delivered the initial `IWorkflowActivityFactory` surface and immutable
`PromptActivityDefinition`/`QuickAnswerActivityDefinition` contracts. The factory is registered
once as scoped by the runtime foundation, receives its kernel, workflow context, and logger
factory explicitly, and creates a fresh mutable activity on every call. Definition construction
performs all authoring-time bounds checks; activity execution remains the existing cancellation
boundary, so composition performs no hidden asynchronous or background work.

### 2.3 Generated adaptive cards retain typed models

The generated-C# path does not need a `dynamic` submission model. ScriptEditor can emit a
normal C# model with nullable annotations and DataAnnotations. ConversaCore supplies an
immutable `GeneratedAdaptiveCardDefinition` and a
`DefinitionAdaptiveCardActivity<TModel>` that renders the definition and uses the existing
typed binding/validation pipeline.

This removes the hand-written `TCard` class and card-factory lambda while preserving:

- compile-time `TModel` identity;
- `ModelContextKey` state storage;
- DataAnnotations validation and field correlation;
- adaptive-card activity IDs and submission routing;
- required-card prompt behavior;
- standard runtime output and UI rendering.

Definitions use an allowlisted input-kind enum rather than arbitrary Adaptive Card JSON.
The first supported kinds are text, number, date, toggle, and choice. Definitions validate
at construction:

- nonblank stable definition and field IDs;
- unique field IDs using ordinal case-insensitive comparison;
- at most 64 fields and 100 choices per choice field;
- field IDs no longer than 128 characters;
- labels, placeholders, and choice labels no longer than 1,024 characters;
- choice values no longer than 256 characters;
- choices only on choice fields and at least one choice for a choice field;
- an Adaptive Card schema version selected by the framework, initially 1.3.

The renderer emits only known element/action shapes and serializes text as data. Generated
definitions cannot inject arbitrary actions, URLs, scripts, templating expressions, or
host commands. Diagnostics may report definition IDs, field counts, and validation codes,
but not submitted values or prompt bodies.

A future runtime-interpreted card registry would require a separate ADR covering trust,
version distribution, persistence, authorization, and schema migration. It is not implied
by this generated-source design.

CC-903 delivered `GeneratedAdaptiveCardDefinition`, immutable field/choice definitions, the
five-value `GeneratedAdaptiveCardInputKind` allowlist, and
`DefinitionAdaptiveCardActivity<TModel>`. Rendering serializes framework-owned Adaptive Card
1.3 shapes only. The activity uses the existing typed binding, DataAnnotations, event, output,
and submission pipeline, but its diagnostics log only activity/model metadata and field counts,
never submitted values. Generated cards check cancellation before rendering and reset to a
runnable `Created` state. Validation-error correlation now compares field IDs without case
sensitivity so CLR names and JSON-style names remain aligned.

## 3. Current activity construction matrix

| Constructor category | Current examples | Generated-C# target |
|---|---|---|
| Literal-safe | `SimpleActivity`, `EndActivity`, `DelayActivity`, `ChoiceActivity`, `FallbackActivity`, `EscalateActivity`, `ResetActivity`, `SignInActivity` | Emit direct constructors and validated property initializers. |
| Optional framework collaborators | `TriggerTopicActivity`, `ShowSuggestionsActivity`, `EventTriggerActivity` | Prefer current direct constructors where omitted dependencies are truly optional; migrate legacy host-event activities under WP7. |
| Semantic service required | `PromptActivity`, `InsuranceDecisionActivity`, `SemanticResponseActivity`, `SemanticQueryActivity` | Use the explicit authoring factory for approved general-purpose shapes; domain-specific or generic semantic activities require dedicated generated strategies. |
| Context/logger required | `QuickAnswerActivity`, `WaitForUserInputActivity`, adaptive-card activities | Use the scoped authoring factory and immutable definitions. |
| Compile-time generic result | `AdaptiveCardActivity<TCard,TModel>`, `InvokeToolActivity<TTool,TRequest,TResult>`, `DecisionActivity<TInput,TEvidence,TResponse>` | Generate concrete model/result types and generic arguments. Do not replace typed tool policy with dynamic invocation. |
| Delegate/control-flow required | `ConditionalActivity`, `SwitchActivity`, `ForEachActivity`, `RepeatActivity`, `ParallelActivity`, `CompositeActivity`, `SimpleActivity` delegate overload | Generate bounded lambdas or child activity expressions from validated graph semantics; this remains a ScriptEditor generation strategy unless a repeated framework-safe definition justifies a factory method. |
| Legacy registry/runtime dependency | `ExecuteTopicActivity`, provider-specific workflow activities | Do not add to the new authoring surface; target registered topic IDs, `TriggerTopicActivity`, typed tools, notifications, and interactions. |

The matrix is intentionally capability-based rather than a promise that every public class
is safe for visual generation. A generated palette must expose only shapes with a specific
compile strategy and tests against the current public contract.

## 4. Verification contract

ConversaCore tests will compile and execute a generated-style topic source shape without
referencing ScriptEditor implementation. The contract must prove:

1. DI constructs the derived topic and its scoped authoring factory.
2. Composition occurs after derived fields are initialized and exactly once.
3. Prompt and quick-answer activities receive collaborators from the correct scope.
4. A generated card renders through standard conversation output and binds a typed model.
5. Invalid definitions fail before conversation execution.
6. Reset, cancellation, disposal, and a second concurrent scope cannot reuse mutable topic,
   activity, card, or context state.
7. ConversaCore.UI renders and submits the generated card through `IConversationRuntime`
   without a domain agent or manual event plumbing.

ScriptEditor#46 should separately compile its emitted syntax against the resulting package.
That cross-repository consumer test is evidence of compatibility, not permission for this
repository to modify ScriptEditor.

## 5. InsuranceAgent and plan impact

InsuranceAgent remains the framework reference application. After framework/UI verification,
its active start and qualification topics are audited for migration to the composed lifecycle
and authoring factory where that makes the supported pattern clearer. Domain-specific card
classes may remain when they provide richer behavior than generated definitions; generated
cards are a supported companion, not a forced loss of domain typing.

WP0-WP5 remain closed historical delivery records. Any changed acceptance requirement is
tracked as an amendment follow-up rather than retroactively claiming the earlier work did not
ship. WP6-WP8 are re-baselined after InsuranceAgent verification so their template, cleanup,
compatibility, security, package, and release gates describe the implemented authoring model.
