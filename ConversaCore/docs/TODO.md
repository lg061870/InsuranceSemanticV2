# ConversaCore Framework TODO List

## Upcoming Features

### UI Communication Components
- [ ] Create `TriggerUXEventActivity` class for signaling UI events
  - Should inherit from `TopicFlowActivity`
  - Allow topics to trigger UI-specific events at key points in the flow
  - Support for dynamic event names (e.g., "LeadInfoEntered")
  - Support for custom event data payloads

- [ ] Add `TopicEventType.UXEvent` to the `TopicEventType` enum
  - Dedicated event type for UI-specific events
  - Distinct from other system events

- [ ] Create UI component subscription helpers
  - Utility methods for UI components to easily subscribe to UX events
  - Filter by event name
  - Type-safe event data handling

### Usage Examples
- [ ] Create sample implementation for lead information workflow
  - Demonstrate "LeadInfoEntered" event
  - Show UI status updates based on event
  
- [ ] Add documentation for UI event pattern
  - Best practices for event naming
  - Recommendations for event payload structure
  - Examples of common UI update scenarios

## Refactoring
- [ ] Review and enhance event cleanup in `TopicEventBus.Terminate()`
  - Ensure all subscribers are properly removed
  - Add diagnostic event to track termination

## Testing
- [ ] Create unit tests for UI event communication
  - Verify events flow correctly from topics to subscribers
  - Test event payload serialization/deserialization
  - Validate event filtering by name works properly

## Documentation
- [ ] Update architecture diagram to include UI event flow
- [ ] Add section on UI communication patterns to Architecture.md