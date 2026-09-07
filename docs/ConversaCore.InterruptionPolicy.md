# Framework interruption and fallback policy (CC-207)

`IConversationMessageCoordinator` composes the catalog router and scoped
workflow runner. It is the framework-only message path that the future public
runtime will use.

1. With no active execution, it routes and activates the selected topic.
2. With a `FirstRefusal` topic waiting, it gives that exact retained instance
   one delivery attempt.
3. If that attempt is handled, no global routing occurs.
4. If it returns `NotHandled`, a second route excludes that descriptor. A
   deterministic, semantic, or explicit-system-fallback selection then runs as
   an interruption.
5. An `Interruptible` topic can be interrupted directly by a qualifying route.
   If no candidate qualifies, the router still returns the active delivery
   decision rather than discarding the waiting topic.
6. The runner retains the suspended original instance; when the interrupting
   workflow completes, it resumes that same instance deterministically.

The coordinator never calls `CanHandleAsync`, scans services, invokes an LLM
directly, or subscribes to legacy events. It receives only the router decision
and immutable execution result. No candidate is activated merely to determine
eligibility.

This does not yet provide reset/cancellation disposal, typed public output,
card/host responses, or the final `IConversationRuntime` facade. Those remain
separate work. No reference-domain application is modified.
