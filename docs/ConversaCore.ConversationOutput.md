# ConversaCore typed conversation output

CC-300 replaces the temporary `object` output stream with an immutable,
domain-neutral `ConversationOutput` hierarchy. Every output carries its conversation
ID and creation timestamp. Payloads contain identifiers, strings, enums, and serialized
card snapshots only; they never expose `TopicWorkflowContext`, topic/activity instances,
services, or domain application types.

The standard UI-facing outputs are `MessageOutput`, `AdaptiveCardOutput`,
`CardStateOutput`, `PromptStateOutput`, `TopicLifecycleOutput`, and
`ActivityLifecycleOutput`. Public state enums are intentionally separate from legacy
event enums so the new API can remain stable while compatibility adapters are retired.

`HostNotificationOutput` and `HostInteractionRequestOutput` establish the two host-event
categories and their common identity/version/correlation metadata. They are abstract in
CC-300: CC-303 adds strongly typed notification payloads, while CC-304 adds typed request
and response payloads plus completion semantics.

`IConversationOutputSubscription.ReadAllAsync` now streams `ConversationOutput` rather
than `object`. Per-conversation ordering, fan-out, cancellation, disposal, and subscriber
isolation are implemented by CC-301; translating legacy activity events is CC-302.
