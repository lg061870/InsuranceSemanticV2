# Framework topic routing (CC-204)

`ITopicRouter` selects from registered descriptors. It never creates a topic, executes
an activity, writes session state, or invokes legacy `CanHandleAsync`. Existing hosts
continue using the legacy path until the new runner and facade are assembled.
InsuranceAgent migration is deferred until the framework is complete and revised
domain requirements are supplied; this feature requires no InsuranceAgent changes.

## Registration

```csharp
builder.Services.AddConversaCoreBuilder(apiKey)
    .AddTopic<AppointmentTopic>("appointments", options =>
    {
        options.TriggerPhrases = new HashSet<string> { "book an appointment" };
    })
    .AddTopic<FallbackTopic>("fallback", options =>
    {
        options.Classification = TopicClassification.System;
    })
    .AddTopicRouting(new TopicRouterOptions { FallbackTopicId = "fallback" });
```

The topic types above are illustrative, not changes to any reference application.
The extension registers a singleton catalog and scoped router; it does **not** register
a complete `IConversationRuntime`. Options/fallback checks occur on router resolution,
not automatically at startup. Aggregated startup validation remains CC-103.

## Selection policy

- The runner supplies the active topic ID and input state. `Waiting` plus `FirstRefusal`
  returns `OfferToActive`. The runner delivers once, then reports `Accepted` or
  `Declined`. `Accepted` returns `AlreadyHandled`, not a request to redeliver.
- `Declined` excludes that topic from ranking for this input. `Interruptible` permits
  ranking before delivery; if nothing qualifies, the waiting topic still gets an offer.
- Only domain descriptors participate in global ranking. An exact trimmed,
  case-insensitive topic ID or declared trigger phrase scores 1. Display names are not IDs.
  Trigger phrases are copied/frozen and may overlap: they are hints, not unique aliases.
- Ties use higher priority, then stable ordinal-ignore-case ID. No registration-order tie.
- If no deterministic match qualifies, an optional `ITopicSemanticRanker` receives at
  most `MaxSemanticCandidates` descriptors (default 20), ordered by priority then ID.
  No ranker means no model call. Empty input skips semantic ranking. The bound is a
  configurable initial limit, not a measured optimal value; CC-804 owns performance tuning.
- Semantic scores must be finite in [0,1], refer only to supplied candidates, and not
  duplicate case-insensitive IDs. Invalid results fail visibly. Highest score wins,
  followed by priority and ID. One inclusive threshold (default 0.5) applies; unlike
  the legacy routers, priority cannot let a weaker semantic score beat a stronger one.
- No match selects only an explicitly configured, registered **system** fallback;
  otherwise returns `NoMatch`. It never selects an arbitrary low-priority domain topic.

The ranker is a provider-neutral opt-in seam; this delivery includes no LLM adapter.
Scorer exceptions and cancellation propagate to the future runtime error boundary.
The router does not log input text or exception payloads. Framework-owned deterministic
matching requires no extra developer service. Semantic configuration is optional.

## Remaining integration

CC-205/207 must consume decisions, offer input once, pause/resume executions, and manage
fallback return. The new facade must serialize operations per conversation; concurrent
router calls alone do not establish end-to-end conversation isolation (CC-212).
Legacy registry/manager removal follows compatibility migration, not this change.
Aliases, declared subtopic references, and lifetime validation remain open under CC-103;
trigger phrases do not silently substitute for those requirements.
