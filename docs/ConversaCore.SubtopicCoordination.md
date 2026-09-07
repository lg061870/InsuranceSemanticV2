# Framework subtopic coordination (CC-206)

When a topic returns a legacy `TopicResult` that requests a subtopic, the
scoped workflow runner—not an activity event subscriber—performs the handoff:

1. Dispatch the parent's immutable `WaitingForSubtopic` outcome.
2. Resolve the requested stable ID in `ITopicCatalog`; reject an unknown ID or
   a cycle before activating anything.
3. Retain the exact parent topic instance in the runner's private stack.
4. Push a session call only if a legacy trigger activity has not already pushed
   the same child reference.
5. Activate and start the child with `ITopicActivator`.
6. On a child terminal outcome, pop the saved parent, pop the runner-owned
   session call, restore that exact instance, and resume it once with the
   conventional compatibility message `"Sub-topic completed"`.

The parent and child are never looked up from a singleton registry. A child
wait remains the active execution; nesting is represented by the runner stack.
Commands remain serialized by the conversation-scoped runner.

## Compatibility and limits

Existing `TriggerTopicActivity` and `CompleteTopicActivity` can separately
push/pop the legacy conversation-context stack. The runner detects a preexisting
child stack reference and does not pop that legacy-owned entry. This isolates
the old behavior while eliminating the legacy `DomainAgentService` event
choreography for new runner calls.

`ITopic` currently supports text input only. Therefore this first compatibility
delivery resumes a legacy parent with the completion message above, rather than
passing its child a typed completion value. Typed continuation data, explicit
card/host response resumption, reset/cancellation cleanup, and interruption
policy are separate tasks. This avoids leaking mutable workflow context to the
runner or presentation layer.
