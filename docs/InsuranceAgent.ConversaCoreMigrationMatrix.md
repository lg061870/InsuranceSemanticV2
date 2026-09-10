# InsuranceAgent → ConversaCore migration matrix

CC-500 baseline for WP5. This document records the current reference application's
orchestration and presentation seams and the target mechanism for each one. It is a
planning artifact only; it does not change InsuranceAgent behavior.

## Current boundaries

| Area | Current evidence | Target owner |
|---|---|---|
| Topic registration | `InsuranceAgent/AddInsuranceTopics.cs:22-174` registers scoped topics through manual factories; startup still populates the legacy registry. | ConversaCore topic descriptors, catalog, and scoped activation. |
| Start/compliance composition | `InsuranceAgent/Services/InsuranceAgentServiceV2.cs:37-49` builds a greeting, inserts `ComplianceTopic`, processes compliance, then appends conditional activities. `:51-70` mutates that flow during start. | Registered `ConversationStartTopic` plus explicit typed subtopic composition; runtime owns activation and reset. |
| Legacy orchestration | Retained compatibility source still contains the former routing/event model, but its async semantic follow-up repairs are removed and application code uses `IConversationRuntime`. | `IConversationRuntime`, `ITopicRouter`, `IWorkflowRunner`, and compatibility adapters only during migration. |
| UI subscription | `InsuranceAgent/Pages/Home.razor:148-170` subscribes to `CustomEventTriggered` and uses `async void`. | `IConversationRuntime.Subscribe()` and one typed `OnHostOutput` boundary. |
| Lead identity | `Home.razor:400-432` creates a lead and stores the returned ID in page field `currentLeadId`. | Lead-creation tool returns a typed lead ID into conversation state. |
| Profile persistence | `Home.razor:448-547` persists life goals, coverage, health, dependents, employment, and beneficiaries from event callbacks using `currentLeadId`. | Typed mutating tools with validated identity, confirmation/idempotency policy where applicable, and typed results. |
| Visual reactions | `Home.razor:187-236` updates progress, opens the customer console, and handles qualification completion. | Standard runtime outputs for generic progress/completion; typed host notifications for site-specific panels. |
| Integrations | `Topics/Demo/ZapierIntegrationDemoTopic.cs` now invokes the registered `ZapierWebhookTool`; `IIntegrationService` is transport-only. | Typed `ZapierWebhookTool` result drives workflow confirmation; visual feedback remains a host notification. |
| Topic identity | The supported start and marketing paths use stable `insurance.*` IDs; startup validates every required descriptor and concrete trigger target. | ConversaCore descriptors and catalog validation. |

## Topic registration and disposition

| Current topic | Migration action |
|---|---|
| `ConversationStartTopic` | Make the registered start descriptor; remove service-owned flow insertion. |
| `ComplianceTopic` | Keep as a registered topic; compose it from the start topic with typed state. |
| `BeneficiaryInfoDemoTopic`, `BeneficiaryRepeatDemoTopic`, `BeneficiaryUserDrivenTopic` | Retain as samples or migrate only if still used by a supported route. |
| `CaliforniaResidentTopic`, `ContactHealthTopic`, `ContactInfoTopic`, `CoverageIntentTopic`, `EmploymentTopic`, `DependentsTopic`, `HealthInfoTopic`, `InsuranceContextTopic`, `LeadDetailsTopic`, `LifeGoalsTopic` | Keep as domain topics; replace page-triggered persistence with tools and explicit typed state. |
| `MarketingT1Topic` | Supported and explicitly registered as `insurance.marketing.t1`; activation awaits `IAsyncInitializable`, and semantic notifications are ordinary ordered activities executed through the framework runner. |
| `MarketingT2Topic` | Supported and explicitly registered as `insurance.marketing.t2`; its downstream topic targets are startup-validated. |
| `EventTriggerDemoTopic`, `SemanticActivitiesDemoTopic`, `HandDownDemoTopic`, `RadioButtonDemoTopic`, `NewbieTopic`, `ZapierIntegrationDemoTopic` | Audit as samples; migrate useful examples to the SDK/sample surface and mark incomplete experiments for later cleanup. |
| T3 path | Explicit no-consent path registered as `insurance.marketing.t3`; it completes without creating a lead or profile data. |

## Host-event and persistence mapping

| Current event/reaction | Target mechanism | Migration task |
|---|---|---|
| `customer_console_show` | Typed host notification (`ShowPanel`) | CC-503, CC-507 |
| `lead_details_submitted` | Lead-creation tool plus progress output | CC-503, CC-504, CC-507 |
| `life_goals_submitted` | Profile persistence tool plus progress output | CC-503, CC-505, CC-507 |
| `coverage_intent_submitted` | Profile persistence tool plus progress output | CC-503, CC-505, CC-507 |
| `health_info_submitted` | Profile persistence tool plus progress output | CC-503, CC-505, CC-507 |
| `dependents_submitted` | Profile persistence tool plus progress output | CC-503, CC-505, CC-507 |
| `employment_submitted` | Profile persistence tool plus progress output | CC-503, CC-505, CC-507 |
| `beneficiaries_submitted` | Profile persistence tool plus progress output | CC-503, CC-505, CC-507 |
| `contact_info_submitted` | Dedicated contact-profile persistence tool using the lead ID returned by lead creation | CC-505 |
| `qualification_complete` | Standard completion/progress output, with optional typed host notification | CC-503, CC-507 |
| Site-specific dialog/question | Correlated host interaction only when a standard prompt/card is insufficient | CC-506 |
| Zapier webhook | Typed `ZapierWebhookTool` result returned to topic | CC-508 |

## Migration order and gates

1. Freeze this matrix and identify the first vertical slice: start/compliance composition.
2. Add typed insurance contracts and tools without removing the compatibility path. Completed for the active T1 path; legacy demo topics remain isolated.
3. Move one lead/persistence path and prove the lead exists without `Home.razor` callbacks. Implemented through deterministic tool activities; E2E proof is tracked by CC-512.
4. Bind `Home.razor` to `IConversationRuntime`, then migrate remaining reactions and persistence. Completed for the active T1 page path.
5. Remove `InsuranceAgentServiceV2` only after end-to-end and two-circuit tests pass.

The reference path no longer subscribes to semantic-completion events or launches delayed
follow-up tasks. Semantic checkpoints and their typed notifications are sequential activities
under the runtime's cancellation and failure boundary.

The matrix is intentionally conservative: legacy services remain until their replacement
behavior is covered, and no reference implementation source is changed by CC-500.
