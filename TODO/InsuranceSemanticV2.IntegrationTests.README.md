## Integration Tests for InsuranceSemanticV2 API

### Overview

This document is a copy of the Integration Tests README used by the `InsuranceSemanticV2.IntegrationTests` project. It summarizes test coverage, failing tests, and running instructions.

### Test Results

**Current Status: 7/12 tests passing (58%)**

#### Passing Tests ✅

1. CreateAgent_ShouldCreateAgentInDatabase_WithAllFields
2. GetAgentLeads_ShouldReturnAssignedLeads
3. UpdateLead_WhenLeadDoesNotExist_ShouldReturn404
4. AddAgentLicense_ShouldCreateLicenseRecord
5. CreateMultipleLeads_ShouldAllBeStoredInDatabase
6. CreateLead_ShouldCreateLeadInDatabase_AndReturnCreatedResponse
7. GetAgents_ShouldReturnAllAgents_WithCorrectMapping

#### Failing Tests ❌

1. GetLead_WhenLeadDoesNotExist_ShouldReturn404
2. UpdateLead_ShouldUpdateDatabaseRecord_AndPreserveCorrectFields
3. GetLead_ShouldReturnLeadFromDatabase_WithCorrectMapping
4. UpdateAgent_ShouldUpdateAllFields_AndPreserveCreatedAt
5. DeleteAgent_ShouldRemoveFromDatabase

### What These Tests Verify

- Database CRUD operations (Create/Read/Update/Delete)
- AutoMapper DTO/entity mapping correctness
- API endpoint behavior and expected HTTP status codes

### Running the Tests

Run all tests:

```
dotnet test
```

Run with verbose output:

```
dotnet test --logger "console;verbosity=detailed"
```

Run a specific test class:

```
dotnet test --filter "FullyQualifiedName~LeadsEndpointsTests"
```

### Test Structure

- IntegrationTestBase: Creates a `WebApplicationFactory<Program>`, configures an in-memory database, and provides `HttpClient` + `DbContext` helpers.
- LeadsEndpointsTests: Lead CRUD tests.
- AgentsEndpointsTests: Agent operations, licenses, appointments, sessions.

### Key Technologies

- xUnit
- FluentAssertions
- WebApplicationFactory
- EF Core InMemory provider

### Next Steps

1. Fix AutoMapper configuration for Lead entity
2. Verify CreatedAt/UpdatedAt handling in update operations
3. Add more edge-case tests
4. Expand coverage to other endpoints (Carriers, Products, Compliance)

