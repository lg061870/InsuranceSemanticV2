# ConversaCore upgrade tracking

Use GitHub issues in `lg061870/InsuranceSemanticV2` as the execution record for the ConversaCore transformation. The master tracker is https://github.com/lg061870/InsuranceSemanticV2/issues/12. WP issues group phases; each CC task has its own native sub-issue. The unrelated MRE issues belong to a separate plan.

Before implementing a CC task, read its issue and the relevant sections of `docs/ConversaCore.TargetArchitecture.md` and `docs/ConversaCore.TransformationWorkBreakdown.md`. Add an issue comment explaining the planned change and why. As work proceeds, document decisions, affected files, commit/PR links, checks and their results, and remaining work or blockers on that task.

Keep partially implemented or only locally validated tasks open. Close an issue only when its entire scope and applicable definition of done are met, implementation is available in GitHub, and verification evidence is recorded. Passing legacy characterization tests reproduce defects; they do not prove the target runtime fixes them. Do not close a WP until every child task and its phase acceptance criteria are satisfied. Update the markdown work breakdown when task scope or completion changes.

Existing baseline failures must be reported explicitly; never describe a failing suite as green or silently waive a completion gate. Refer cross-cutting follow-up work to its own CC issue.
