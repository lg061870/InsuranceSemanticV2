# Framework reset and cancellation (CC-208)

The scoped workflow runner owns one cancellation generation. Every activation,
topic execution, and internal output dispatch receives a token linked to that
generation and the caller token.

`CancelAsync` signals the generation before waiting for the active serialized
operation to unwind, then clears retained executions and the active descriptor.
`ResetAsync` performs the same cancellation and additionally calls the public
conversation-session reset operation. Both replace the cancellation generation
only after exclusive runner ownership is restored. No reflection is used.

Reset does not select or start a topic: that policy belongs to the future public
runtime facade. External tools, host interactions, and remaining legacy event
work have separate cancellation integrations. No domain application changes are
part of this framework task.
