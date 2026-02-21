## ConversaCore Activity Reference for TopicManager RAG

This document describes the concrete activity types currently used in ConversaCore topics. It is written for an AI agent that reads *prose* from a conversation designer and must map that prose to the correct ConversaCore activity types and parameters.

The agent should treat the sections below as the **source of truth** for what each activity does, when to use it, and what arguments are typically required.

### General Concepts

- **Topic** – a class derived from `TopicFlow` that builds a FIFO queue of activities, usually using `Add(...)`.
- **Activity** – a unit of work in a topic (send a message, ask a question, show a card, call a sub-topic, etc.).
- **Context** – the `TopicWorkflowContext` used to read/write variables (e.g., `ctx.GetValue<T>(key)`, `ctx.SetValue(key, value)`).

When the conversation designer describes a step in natural language, the AI should:

1. Decide **which activity type** best matches that step.
2. Infer the **activity id** (a short, stable string identifier).
3. Infer the **key parameters** (text, options, context keys, etc.).

Below, each activity is described in a way that supports this mapping.

---

## 1. SimpleActivity

**Purpose**  
Generic, flexible activity for inline logic or static messages. It is the default choice when the designer wants to:
- Send a static text message.
- Run a small piece of C# logic (read/write context, perform simple calculations).

**Common constructor patterns**

1. **Static text message**
	 - Shape: `new SimpleActivity(id, messageText)`
	 - Use when the designer says things like:
		 - “Show a welcome message: ‘Welcome to our insurance assistant.’”
		 - “Tell the user we are preparing their summary.”

2. **Inline logic / lambda**
	 - Shape: `new SimpleActivity(id, (ctx, input) => { ...; return Task.FromResult<object?>(result); })`
	 - Use when the designer describes logic such as:
		 - “Store the user’s age in context.”
		 - “Look up some values and compute a message.”
		 - “Read a model from context and format a summary sentence.”

**AI mapping hints**

- If the prose describes **one chat message** with no input collection, prefer `SimpleActivity`.
- If the prose describes **small context manipulations** (set/get a few variables) without external calls or cards, also use `SimpleActivity`.

---

## 2. QuickAnswerActivity

**Purpose**  
Ask the user a **single question** with a **small fixed set of button-style answers**. Stores the selection in context as a simple model (commonly read via `GetModelProperty("ActivityId", "answer", ...)`).

**Common constructor pattern**

- Shape:
	- `new QuickAnswerActivity(id, question, answers[], context, logger)`

**Typical usage in prose**

- “Ask the user if they want to continue or exit, with buttons: ‘Yes, continue’ and ‘No, exit’.”
- “Offer three choices: ‘New quote’, ‘Learn basics’, ‘Talk to agent’.”

**AI mapping hints**

- If the designer explicitly mentions **buttons, choices, or quick replies**, use `QuickAnswerActivity`.
- The **question** becomes the `question` parameter.
- The **options list** becomes the `answers` array.
- The result will typically be consumed later via `GetModelProperty("<id>", "answer", ...)` in conditions.

---

## 3. AdaptiveCardActivity<TCard, TModel>

**Purpose**  
Render a rich **Adaptive Card** to collect structured input (forms), then validate and store the model into context.

**Common constructor patterns**

1. **Basic form card**
	 - Shape:
		 - `new AdaptiveCardActivity<SomeCard, SomeModel>(id, context, cardFactory)`
	 - `cardFactory` creates the card instance, often using current context values.

2. **With explicit model context key + logger**
	 - Shape (from demo topics):
		 - `new AdaptiveCardActivity<ZapierTestCard, ZapierTestModel>(id, context, cardFactory, modelContextKey, logger)`

**Typical usage in prose**

- “Show a form to collect contact information (name, email, phone).”
- “Display a card where the user can enter their dependents and income details.”
- “Ask for TCPA consent using a card with checkboxes and confirm button.”

**AI mapping hints**

- If the designer describes a **form-like UI** with labeled fields (name, email, sliders, toggles), map to `AdaptiveCardActivity<SpecificCard, SpecificModel>`.
- The **TCard** and **TModel** will usually be an existing pair (e.g., `ContactInfoCard` / `ContactInfoModel`, `HealthInfoCard` / `HealthInfoModel`).
- The AI should:
	- Use the **card name** that best matches the described intent (e.g., “contact info card”).
	- Ensure there is a **model context key** if the design implies the data will be reused later.

---

## 4. WaitForUserInputActivity

**Purpose**  
Pause the flow and wait for a **free-form text reply** from the user, without showing a full card.

**Common constructor pattern**

- Shape:
	- `new WaitForUserInputActivity(id, context, logger, prompt)`

**Typical usage in prose**

- “Ask the user to type any question in their own words.”
- “Prompt the user: ‘What would you like to say?’ and wait for their response.”

**AI mapping hints**

- When the designer wants **open text** input with a simple prompt (no structured options, no full card), choose `WaitForUserInputActivity`.
- The prompt sentence becomes the `prompt` parameter.
- The user’s response is usually stored under a key like `LastUserMessage`.

---

## 5. ShowSuggestionsActivity

**Purpose**  
Render a list of **non-blocking suggestions** (chips/shortcuts) that the user can click to continue the conversation.

**Common constructor pattern**

- Shape:
	- `new ShowSuggestionsActivity(id, suggestions[])`

**Typical usage in prose**

- “After answering, show suggestions like: ‘insurance basics’, ‘coverage guides’, ‘FAQs’.”
- “Offer a few suggestion chips to help the user continue.”

**AI mapping hints**

- Use `ShowSuggestionsActivity` when suggestions are meant as **hints or shortcuts**, not required responses.
- The suggestions list maps directly to the `suggestions` array.

---

## 6. ChatPromptAttentionActivity

**Purpose**  
Temporarily **highlight or animate** the bottom chat prompt (attention effect) with an optional message, to nudge the user to type something.

**Common constructor pattern**

- Shape:
	- `new ChatPromptAttentionActivity(id, message, durationMs, logger)`

**Typical usage in prose**

- “Flash the chat prompt for 4 seconds with a message like ‘Please elaborate on your last answer.’”
- “Visually ping the prompt so the user knows where to type.”

**AI mapping hints**

- When the designer explicitly talks about **drawing attention to the input box** or a “prompt attention demo”, choose `ChatPromptAttentionActivity`.
- `durationMs` is typically a small number of seconds in milliseconds (e.g., 3000–5000).

---

## 7. EndActivity

**Purpose**  
Explicitly **end** the current topic (and often send a final message).

**Common constructor patterns**

1. **Silent end**
	 - `new EndActivity(id)`

2. **End with message**
	 - `new EndActivity(id, messageText)`

**Typical usage in prose**

- “If the user says ‘No, exit’, end the flow with a polite goodbye message.”
- “Terminate the topic after we’ve sent their educational resources.”

**AI mapping hints**

- Whenever the designer describes a **terminal state** for the topic (no further activities) and mentions a **final message**, map to `EndActivity`.
- If no final text is mentioned, use the no-message constructor.

---

## 8. TriggerTopicActivity

**Purpose**  
Call another **sub-topic** from within the current topic. Optionally wait for the sub-topic to complete (hand-down/regain-control pattern).

**Common constructor patterns**

1. **Simple trigger without explicit wait**
	 - `new TriggerTopicActivity(id, topicName, logger)`

2. **With `waitForCompletion` and conversation context** (as seen in hand-down demos):
	 - `new TriggerTopicActivity(id, topicName, logger, waitForCompletion: true, conversationContext)`

**Typical usage in prose**

- “Now call the Coverage Estimate topic, then come back here and continue.”
- “Delegate to a ‘Life Goals’ sub-topic to collect more details.”

**AI mapping hints**

- If the designer speaks about **jumping to another named topic** (e.g., “CoverageEstimateTopic”, “QuoteIntakeTopic”), use `TriggerTopicActivity`.
- If they explicitly want to **resume current topic after the sub-topic**, set `waitForCompletion: true` and pair it later with a `CompleteTopicActivity` in the sub-topic.

---

## 9. ConditionalActivity<TActivity>

**Purpose**  
Dynamic routing: choose which activity implementation to run based on context, using a **switch-like branching**.

**Common factory pattern**

- Shape:
	- `ConditionalActivity<TActivity>.Switch(id, selectorFunc, branchesDictionary, defaultBranch, logger)`
	- Example usage in topics:
		- `ConditionalActivity<ZapierWebhookActivity>.Switch(...)`
		- `ConditionalActivity<TriggerTopicActivity>.Switch(...)`

**Typical usage in prose**

- “If the user chose ‘Trigger test Zap’, then run the Zap activity; otherwise, skip it.”
- “Based on a score, route to the high-priority topic, needs-education topic, or default topic.”

**AI mapping hints**

- When the designer describes a **branching decision that selects different activities**, not just a single `if` around an Add, map this to `ConditionalActivity<T>`.
- The **selector** typically returns a string key (like `"run"`, `"high_priority"`).
- The **branches dictionary** maps keys to functions that create the corresponding activity instance.

Note: simpler boolean gating (“if condition then run this single activity”) is often expressed via helper wrappers (e.g., `FlowConditionHelpers.IfCase`) that still ultimately produce an activity, but for RAG purposes you can treat those as conditional wrappers around the underlying activity type.

---

## 10. ZapierWebhookActivity

**Purpose**  
Send a payload to a **Zapier webhook** and optionally wait for a response.

**Common construction pattern** (used via `ConditionalActivity` in demo topics)

- Shape inside a branch factory:
	- `new ZapierWebhookActivity(id, integrationService, logger) { WebhookUrlContextKey = ..., DataContextKey = ..., ResponseContextKey = ..., EventType = ..., WaitForResponse = true/false }`

**Typical usage in prose**

- “Send the collected phone number and message to Zapier and wait for the response.”
- “Trigger a test Zapier workflow using data in context.”

**AI mapping hints**

- When the designer explicitly references **Zapier** or “webhook to Zapier”, use `ZapierWebhookActivity`.
- Fields usually come from context keys like `zapier_webhook_url`, `zapier_data`, etc.

---

## 11. WhatsAppMessageActivity

**Purpose**  
Send a message to **WhatsApp** via an integration, often driven by data collected earlier.

**Typical construction pattern** (similar to Zapier, not always via Add):

- Created inside a `ConditionalActivity` or simple logic that builds a new `WhatsAppMessageActivity(id, integrationService, logger)` and configures context-related properties.

**Typical usage in prose**

- “Send a WhatsApp test message to the number the user gave us.”

**AI mapping hints**

- When the designer clearly wants to **send something via WhatsApp**, choose `WhatsAppMessageActivity`.
- Expect associated context keys for phone number, template, and message body.

---

## 12. SemanticResponseActivity

**Purpose**  
Call Semantic Kernel / LLM to generate a response based on **knowledge base content** or **prompt templates** (e.g., education modules, FAQ answering).

**Common construction patterns**

- Direct:
	- `new SemanticResponseActivity(id, kernel, logger, ...)`
- Wrapped by higher-level helpers, e.g. `LifeInsuranceBasicsModule.CreateActivity(...)` which returns a configured `SemanticResponseActivity`.

**Typical usage in prose**

- “Generate a short overview of life insurance basics from our learning library.”
- “Answer the user’s follow-up question using the knowledge base.”

**AI mapping hints**

- If the step is **LLM-powered explanation or Q&A** that doesn’t involve structured UI, pick `SemanticResponseActivity`.
- Prefer using existing modules (like `LifeInsuranceBasicsModule`) when the prose clearly aligns with them.

---

## 13. PromptActivity

**Purpose**  
Represent a reusable **prompt-based decision or message generator** driven by Semantic Kernel, often used for conditional logic or summarization.

**Common construction patterns**

- Simple prompt:
	- `new PromptActivity(id, kernel, logger)` (with additional configuration for the question and answer type).

**Typical usage in prose**

- “Use AI to decide what the next best action is based on the context.”
- “Summarize the user’s coverage needs in one sentence.”

**AI mapping hints**

- When the designer describes a **semantic decision or summarization step** that will later influence routing, consider `PromptActivity`.
- Often paired with a follow-up `SimpleActivity` or conditional logic that interprets the result.

---

## 14. DecisionActivity<TInput, TEvidence, TResponse>

**Purpose**  
Generic **AI decision engine** that takes a typed input, optional evidence documents, and produces a structured response (decision result).

**Common construction pattern**

- Shape (from demo topic):
	- `new DecisionActivity<LeadInfo, DocumentEvidence, InsuranceDecisionResponse>(id, kernel, logger, ...)`

**Typical usage in prose**

- “Run a more advanced AI decision step that looks at lead info plus document evidence and decides on a recommendation.”

**AI mapping hints**

- When the designer talks about **non-trivial, multi-signal decision making** (combining lead data + documents + other signals), map to `DecisionActivity<...>`.
- Input, evidence, and response types should be chosen based on the described domain types.

---

## 15. EventTriggerActivity

**Purpose**  
Trigger a **UI event** or an external event to the host application (Blazor UI, dashboards, etc.), optionally waiting for a response.

**Common construction patterns**

- Simple notification:
	- `new EventTriggerActivity(id, eventName, payloadSelector, logger, fireAndForget: true/false)`

**Typical usage in prose**

- “Notify the UI that lead qualification is complete so it can update the progress bar.”
- “Trigger an event to open the customer console after lead details are captured.”

**AI mapping hints**

- Whenever the designer mentions **UI updates, events, or notifications** not directly visible as chat messages, pick `EventTriggerActivity`.
- The `eventName` should be a short, stable string (e.g., `LeadQualificationComplete`).

---

## 16. CompleteTopicActivity

**Purpose**  
Signal that a **topic has completed** and optionally provide completion data back to a parent topic when using the hand-down/regain-control pattern.

**Common construction patterns**

- Simple completion:
	- `new CompleteTopicActivity(id, completionData)`

**Typical usage in prose**

- “When the sub-topic is done collecting beneficiary info, mark it complete and return the collected model to the parent.”

**AI mapping hints**

- Use `CompleteTopicActivity` in **sub-topics** that are invoked via `TriggerTopicActivity(waitForCompletion: true)`.
- Completion data is usually stored in context under a key the parent expects.

---

## 17. DumpCtxActivity

**Purpose**  
Development-only utility to **dump the current context** to logs or UI for debugging.

**Common construction pattern**

- Shape:
	- `new DumpCtxActivity(id, isDevelopmentFlag)`

**Typical usage in prose**

- “For debugging, output the full context after this step.”

**AI mapping hints**

- Only use `DumpCtxActivity` when the designer explicitly wants **debug output** of context.
- This should not be used in production-only flows unless clearly requested.

---

## 18. GlobalVariableActivity

**Purpose**  
Read or write **global-scope variables** that outlive a single topic execution (e.g., store a model globally for reuse).

**Common construction pattern**

- From demo topics:
	- `new GlobalVariableActivity(id, key, valueSelector, logger)` (exact shape may vary, but the intent is to push a value into global context).

**Typical usage in prose**

- “Save the selected beneficiary list as a global variable so other topics can use it.”

**AI mapping hints**

- When the designer explicitly talks about **global variables** shared across topics or sessions, use `GlobalVariableActivity`.

---

## 19. SemanticQueryActivity<TRules, TInput, TResult>

**Purpose**  
Run a **semantic query** that combines conventional rules (TRules) with LLM reasoning to produce a typed result (TResult) from an input model (TInput). Often used for qualification, carrier selection, or similar tasks.

**Common construction pattern**

- Shape (from marketing topics):
	- `new SemanticQueryActivity<RuleSetType, InputModel, ResultModel>(id, kernel, logger, ...)`

**Typical usage in prose**

- “Given the lead’s data and some underwriting rules, determine which carriers are qualified.”
- “Use rules plus AI to classify this lead’s risk tier.”

**AI mapping hints**

- Where the designer mentions **rules + AI** or “semantic qualification/eligibility”, pick `SemanticQueryActivity<...>`.
- TRules is typically a strongly-typed rule set (e.g., `CombinedInsuranceRuleSet`).

---

## 20. RepeatActivity.UserPrompted<TActivity>

**Purpose**  
Allow the user to **repeat** a given activity (such as a card) multiple times, under user control (e.g., adding multiple beneficiaries).

**Common usage pattern**

- Factory-like construction:
	- `RepeatActivity.UserPrompted<AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>>(... )`
	- Inside, a function builds the repeated activity instance:
		- `() => new AdaptiveCardActivity<BeneficiaryInfoCard, BeneficiaryInfoModel>(...)`

**Typical usage in prose**

- “Let the user add one or more beneficiaries; after each card submission, ask if they want to add another, and repeat until they say they’re done.”

**AI mapping hints**

- When the designer describes **user-driven repetition** of the same step (e.g., “add another X?” loops), model it with `RepeatActivity.UserPrompted<T>` around the underlying activity type.
- The repeated inner activity is usually an `AdaptiveCardActivity` for a single item.

---

## How the AI Should Use This Guide

When converting prose to ConversaCore code for TopicManager:

1. **Identify intent** – Is the step about messaging, input collection, decision, sub-topic delegation, UI event, or external integration?
2. **Select activity type** – Use the sections above to pick the closest match.
3. **Infer parameters** – Derive `id`, text, options, card/model types, and context keys from the prose.
4. **Respect patterns** – Use `TriggerTopicActivity` + `CompleteTopicActivity` for hand-down/regain control; use `QuickAnswerActivity` for button choices; use `AdaptiveCardActivity` for forms.

This guide is optimized so an AI agent can robustly and consistently translate natural-language conversation designs into valid ConversaCore topic flows.
