# Topic Chaining Architecture - TODOs

## Overview
This document tracks the architectural enhancements needed for proper topic chaining and sub-topic execution patterns, inspired by Copilot Studio's `BeginDialog` model.

## Current State
- ✅ **Hand-off Pattern**: Topics can transfer control to other topics via `TriggerTopicActivity`
- ✅ **Global Variable Initialization**: Insurance-specific global variables established before topic execution
- ✅ **Basic Topic Registry**: Topics can be discovered and invoked by name
- ❌ **Return Control**: Calling topic cannot regain control after sub-topic completes
- ❌ **Sub-routine Model**: No call stack or execution context preservation

## Priority 1: Hand-down → Regain Flow Control

### Problem Statement
Currently topics follow a **sequential hand-off pattern**:
```
TopicA → Add(TriggerTopicActivity("TopicB")) → TopicB starts, TopicA dies
```

We need a **sub-routine pattern**:
```
TopicA → Call TopicB → TopicB completes → TopicA resumes next activity
```

### Proposed Solution: Enhanced TriggerTopicActivity
1. **New Parameter**: `waitForCompletion: bool`
2. **Execution Context Preservation**: Calling topic pauses, doesn't terminate
3. **Return Mechanism**: Sub-topic signals completion, control returns to caller
4. **Activity Queue Resumption**: Calling topic continues with next activity in queue

### Implementation Areas
- [ ] Enhance `TriggerTopicActivity` with completion waiting
- [ ] Add topic execution stack to `IConversationContext`
- [ ] Modify topic lifecycle to support pause/resume
- [ ] Update `TopicManager` to handle nested topic execution
- [ ] Add completion signaling mechanism

## Priority 2: Context Management Rules

### Global Variable Update Policy
**Rule**: Only `SetVariableActivity` can update global context variables.

### Rationale
- **Centralized Control**: Single point of context modification
- **Debugging**: Easy to track when/where global state changes
- **Consistency**: Enforces structured approach to state management
- **Validation**: Can add validation logic to `SetVariableActivity`

### Implementation
- [x] Create `SetVariableActivity` class
- [x] Implement global variable update logic
- [x] Add validation and logging
- [ ] Update existing code to use `SetVariableActivity` instead of direct context calls
- [ ] Consider making `IConversationContext.SetValue()` internal/protected for global variables

## Priority 3: Cycle Detection (Future)

### Problem Statement
Topic graphs may contain cycles:
```
ConversationStart → TCPA → LifeGoals → BackToStart (cycle)
```

### Considerations for Later
- [ ] Maximum call stack depth
- [ ] Cycle detection algorithm
- [ ] Break-out mechanisms
- [ ] Performance implications

## Priority 4: Advanced Return Values (Future)

### Current Approach
Sub-topics update global variables in context, calling topic reads updated globals.

### Future Enhancements
- [ ] Typed return values from sub-topics
- [ ] Error/exception propagation
- [ ] Conditional branching based on sub-topic results

## Implementation Notes

### Architecture Principles
1. **Backward Compatibility**: Existing `TriggerTopicActivity` behavior preserved
2. **Incremental Enhancement**: New features opt-in via parameters
3. **Global Variable Discipline**: Structured approach to context management
4. **Framework Neutrality**: ConversaCore remains domain-agnostic

### Key Files to Modify
- `ConversaCore/TopicFlow/Activities/TriggerTopicActivity.cs`
- `ConversaCore/Context/IConversationContext.cs`
- `ConversaCore/Services/TopicManager.cs` (if exists)
- `ConversaCore/TopicFlow/Activities/SetVariableActivity.cs` (new)

### Testing Strategy
- [ ] Unit tests for nested topic execution
- [ ] Integration tests for topic call stack
- [ ] Performance tests for deep nesting
- [ ] Error handling tests for malformed topic chains

## Questions for Resolution
1. Should `waitForCompletion` be default `true` or `false`?
2. What's the maximum recommended topic nesting depth?
3. Should we implement timeout mechanisms for long-running sub-topics?
4. How should we handle exceptions in sub-topics?

---
*Document created: October 8, 2025*
*Last updated: October 8, 2025*