# NEEDS ANALYSIS

**Status:** Not currently part of the application. This is a concept under consideration and requires further analysis.

## Idea: Click-to-Call Integration Between LiveAgent Console and Customer Console

### Overview
Enable click-to-call functionality that bridges the LiveAgent console and the Customer console (InsuranceAgent), allowing agents to initiate calls with customers who are currently online in the system.

### Proposed User Flow

#### LiveAgent Console (Agent Side)
1. Agent clicks on a lead in the grid
2. Lead is loaded into the CALL section at the top
3. Call Button is enabled **if** a phone number exists for that lead
4. Agent can click the Call button to initiate a call request

#### Customer Console (InsuranceAgent - Customer Side)
1. Customer sees a "TabLiveAgent" tab with a counter
2. When an agent loads the customer's profile:
   - The tab title counter changes (increments)
   - The tab becomes enabled/active
   - A card for the agent becomes visible to the customer
3. When agent clicks Call button:
   - Customer sees a visual hint/notification that the agent wants to talk
   - Customer can accept or reject the call

### Technical Challenges to Analyze

#### 1. **Live User Tracking**
- How do we determine if a customer is currently online?
- Need to track active sessions in the InsuranceAgent application
- Differentiate between:
  - **Active/Live leads** - Customer is currently online in the system
  - **To-Rescue leads** - Customer may no longer be online, requires traditional phone call

#### 2. **Real-Time Communication**
- SignalR already in use for lead updates
- Need bidirectional communication:
  - Agent → Customer: Call initiation request
  - Customer → Agent: Accept/Reject response
- Consider: Hub design, connection management, reconnection logic

#### 3. **Call State Management**
- Track call states: Idle → Ringing → Active → Ended → Rejected
- Handle edge cases:
  - Customer goes offline during call request
  - Agent cancels call before customer responds
  - Multiple agents trying to call same customer
  - Timeout for unanswered call requests

#### 4. **UI/UX Considerations**
- **LiveAgent Console:**
  - Visual feedback when call is pending (ringing state)
  - Call controls: End call, mute, etc.
  - Fallback to traditional phone call for offline customers

- **Customer Console (InsuranceAgent):**
  - Non-intrusive notification for incoming call
  - Accept/Reject UI
  - Active call indicator
  - Agent information display

#### 5. **Lead Availability Logic**
- **For "Active/Live" leads:**
  - Prefer in-app call (WebRTC or similar)
  - Customer must be authenticated and have active session

- **For "To-Rescue" leads:**
  - These customers are likely not online
  - Fall back to traditional phone call (PSTN)
  - May need integration with telephony provider

#### 6. **Session Tracking**
- Track customer sessions in database
- Session table might include:
  - CustomerId/LeadId
  - SessionId (SignalR connection ID)
  - LastActivityTimestamp
  - IsOnline (boolean)
  - Current page/topic context

#### 7. **Security & Privacy**
- Ensure agent can only initiate calls with authorized leads
- Customer consent for in-app calls
- Call logging and audit trail

#### 8. **Data Model Changes Needed**
- **CallSession table:**
  - CallSessionId
  - LeadId
  - AgentId
  - InitiatedAt
  - Status (Pending, Active, Completed, Rejected, Timeout)
  - CallType (InApp, PSTN)
  - EndedAt

- **UserSession/CustomerSession table:**
  - SessionId
  - LeadId (or CustomerId)
  - ConnectionId (SignalR)
  - StartedAt
  - LastActivityAt
  - IsActive

#### 9. **Integration Points**
- SignalR hubs: LeadsHub (already exists), may need CallHub
- API endpoints for call management
- Possible WebRTC integration for voice calls
- Telephony API for offline customers

### Open Questions
1. Should we use WebRTC for in-app voice calls, or just use it as a notification mechanism to coordinate traditional phone calls?
2. How long should a call request stay pending before timing out?
3. Can multiple agents see the same lead? If so, how do we handle concurrent call attempts?
4. Should we track "To-Rescue" leads differently in terms of availability?
5. Do we need a queue system if customer is already on a call with another agent?
6. What happens if customer loses internet connection during an active call?

### Next Steps (When Ready to Implement)
1. Design session tracking mechanism
2. Design SignalR message contracts for call flow
3. Create database migrations for CallSession and UserSession tables
4. Prototype call flow UI in both consoles
5. Implement availability detection logic
6. Handle edge cases and error scenarios
7. Add call logging and analytics

---

**Note:** This is a conceptual design that requires significant architecture analysis before implementation. Not currently scheduled for development.
