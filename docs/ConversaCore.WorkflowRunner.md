# Framework workflow runner (CC-205)

`IWorkflowRunner` is scoped to one conversation. It is the only layer in the
new runtime permitted to retain a mutable active `ITopic` instance. The session
stores only the corresponding immutable `TopicDescriptor`.

## Operations

- `StartAsync(descriptor)` activates a new topic and invokes its legacy
  `ProcessMessageAsync` compatibility seam with an empty start input.
- `ActivateAndDeliverAsync(descriptor, message)` activates the selected topic
  and delivers a message after routing has selected it.
- `DeliverToActiveAsync(message)` uses the already retained instance; it does
  not resolve a replacement from DI.

The runner serializes these operations for its scoped conversation. It awaits
activation, topic execution, and dispatch. It does not call `CanHandleAsync`,
subscribe to activity events, expose a mutable workflow context, or allow UI
or domain code to own execution state.

`TopicResult` is translated into an immutable `WorkflowExecutionOutcome`:

| Legacy outcome | Runner state | Retains active instance |
|---|---|---|
| `RequiresInput` or `KeepActive` | `WaitingForInput` | Yes |
| Subtopic request | `WaitingForSubtopic` | Yes |
| Completed or handled terminal result | `Completed` | No |
| Unhandled result | `NotHandled` | No |

The internal `IWorkflowOutputDispatcher` is an awaitable, context-free handoff
so runner code does not recreate raw .NET event chains. It is not the public
output contract: CC-300 through CC-302 replace it with typed, buffered
conversation output and subscriptions.

## Deliberate boundaries

CC-206 owns pushing/popping the topic call stack and resuming a parent after a
subtopic. CC-207 decides whether a new activation may interrupt a retained
one. CC-208 owns reset/cancellation cleanup, CC-209 awaits real-topic
initialization, CC-210 removes legacy async event paths, and WP3 owns public
typed output, cards, and host interactions. `IConversationRuntime` assembly
also remains later work.

This is framework-only. No InsuranceAgent code is changed; reference-domain
migration waits for the completed framework and revised domain requirements.
