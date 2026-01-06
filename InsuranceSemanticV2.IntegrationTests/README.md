# Integration Tests for InsuranceSemanticV2 API

## Overview

This project contains integration tests that verify the API endpoints work correctly with a real database. The tests use an in-memory database to ensure fast, isolated test execution without affecting production data.

## Test Results

**Current Status: 7/12 tests passing (58%)**

### Passing Tests ✅

1. **CreateAgent_ShouldCreateAgentInDatabase_WithAllFields** - Verifies agent creation with all fields mapped correctly
2. **GetAgentLeads_ShouldReturnAssignedLeads** - Verifies agent-lead relationship queries work
3. **UpdateLead_WhenLeadDoesNotExist_ShouldReturn404** - Verifies proper 404 handling for missing leads
4. **AddAgentLicense_ShouldCreateLicenseRecord** - Verifies license records are created and linked to agents
5. **CreateMultipleLeads_ShouldAllBeStoredInDatabase** - Verifies batch lead creation
6. **CreateLead_ShouldCreateLeadInDatabase_AndReturnCreatedResponse** - Verifies lead creation with AutoMapper
7. **GetAgents_ShouldReturnAllAgents_WithCorrectMapping** - Verifies agent retrieval and mapping

### Failing Tests ❌

1. **GetLead_WhenLeadDoesNotExist_ShouldReturn404** - Returns 500 instead of 404 (AutoMapper issue)
2. **UpdateLead_ShouldUpdateDatabaseRecord_AndPreserveCorrectFields** - AutoMapper configuration issue
3. **GetLead_ShouldReturnLeadFromDatabase_WithCorrectMapping** - AutoMapper mapping issue
4. **UpdateAgent_ShouldUpdateAllFields_AndPreserveCreatedAt** - Field mapping issue
5. **DeleteAgent_ShouldRemoveFromDatabase** - Deletion test failing

## What These Tests Verify

### Database Operations
- **Create**: Records are properly inserted into the database
- **Read**: Records can be retrieved and mapped correctly
- **Update**: Existing records can be modified while preserving audit fields
- **Delete**: Records can be removed from the database

### AutoMapper Integration
- DTOs are correctly mapped to entities
- Entity properties are preserved during mapping
- Relationships between entities are maintained

### API Endpoint Behavior
- HTTP status codes are correct (201 Created, 200 OK, 404 Not Found)
- Response payloads contain expected data
- Error handling works properly

## Running the Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~LeadsEndpointsTests"
```

## Test Structure

### IntegrationTestBase
Base class that:
- Creates a `WebApplicationFactory<Program>` for hosting the API
- Configures an in-memory database for each test
- Provides `HttpClient` for making API requests
- Provides `DbContext` for verifying database state

### Test Organization
- **LeadsEndpointsTests**: Tests for lead CRUD operations
- **AgentsEndpointsTests**: Tests for agent operations, licenses, appointments, and sessions

## Key Technologies

- **xUnit**: Test framework
- **FluentAssertions**: Fluent assertion library for readable test assertions
- **WebApplicationFactory**: In-memory test server for integration testing
- **Entity Framework InMemory**: In-memory database provider for fast tests

## Next Steps

To achieve 100% test coverage:
1. Fix AutoMapper configuration for Lead entity
2. Verify CreatedAt/UpdatedAt handling in update operations
3. Add more test scenarios for edge cases
4. Add tests for other endpoints (Carriers, Products, Compliance, etc.)
