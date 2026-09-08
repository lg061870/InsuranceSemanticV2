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
CC-300. `HostNotification<TPayload>` supplies the CC-303 one-way contract: it freezes the
serializable typed payload at construction and returns fresh typed values from an immutable
JSON snapshot, preventing caller/consumer mutation from changing dispatched data. CC-304
adds typed interaction request and response payloads plus completion semantics.

`IConversationOutputSubscription.ReadAllAsync` now streams `ConversationOutput` rather
than `object`. The scoped `ConversationOutputDispatcher` serializes publication and fans
each output out to independent unbounded subscription channels. Publication never invokes
or waits for consumer code. Cancelling or disposing a subscription removes only that
reader; disposing the dispatcher completes all readers. Outputs for a different
conversation are rejected before publication. Translating legacy activity events is
CC-302.

`LegacyTopicOutputAdapter` is the temporary CC-302 bridge for existing `TopicFlow`
implementations. An attachment lease subscribes to topic and activity lifecycle, message,
and adaptive-card events, snapshots them into typed outputs, and feeds an ordered internal
channel serviced by one tracked dispatch pump. The lease owns and removes every handler.
It reproduces the legacy card sequence—previous card read-only, current card active, card
payload, then prompt state—without forwarding mutable workflow context. The concrete
runtime created during UI binding (CC-306) owns attachment and lease disposal.
