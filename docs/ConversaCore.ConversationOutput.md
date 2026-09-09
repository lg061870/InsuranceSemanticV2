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

`HostOutput` is the common marker for the only domain-specific UI hook.
`HostNotificationOutput` and `HostInteractionRequestOutput` establish its two host-event
categories and their common identity/version/correlation metadata. They are abstract in
CC-300. `HostNotification<TPayload>` supplies the CC-303 one-way contract: it freezes the
serializable typed payload at construction and returns fresh typed values from an immutable
JSON snapshot, preventing caller/consumer mutation from changing dispatched data.

`HostInteractionRequest<TRequest,TResponse>` supplies the correlated two-way contract.
`HostInteractionCoordinator` allocates a unique request ID, registers it in the scoped
session, dispatches the frozen typed request, and awaits exactly one response matching the
declared type. Wrong-type replies are rejected without consuming the request; unknown,
duplicate, expired, and late replies throw `HostInteractionNotPendingException`. Timeout,
caller cancellation, dispatch failure, and coordinator disposal all remove pending session
state. Concurrent requests are resolved only through their own correlation IDs.

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

CC-305 extends that compatibility lease to legacy `EventTriggerActivity` instances.
Fire-and-forget activities are translated to
`HostNotification<LegacyEventTriggerPayload>` and complete once the immutable output has
been accepted by the dispatcher; they never wait for a host consumer. Wait-for-response
activities are translated to
`HostInteractionRequest<LegacyEventTriggerPayload, JsonElement>` and resume only after
the host submits the matching request ID through `IHostInteractionCoordinator`.
Timeout and cancellation clear both correlation state and legacy waiting markers. The
adapter logs a structured warning on every use and detaches its runtime handler with the
lease. New topics should use domain-owned typed contracts instead of this retirement-only
envelope.

ConversaCore.UI exposes those two forms through one `OnHostOutput` callback. Its
`ConversationHostOutputContext` carries the immutable `HostOutput` and provides a typed
`RespondAsync` operation only when the output is an interaction request. Responses retain
the request ID and flow through `IConversationRuntime`; responding to a notification is
rejected locally. Host outputs are not inserted into the generic chat transcript.
