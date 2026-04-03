---
description: "Use when: working with .NET backend code, domain entities, controllers, infrastructure, or tests. Provides guidance on domain immutability, EF configurations, sealed classes, and testing patterns."
applyTo: ["src/**/*.cs", "tests/**/*.cs"]
---

## .NET Backend Instructions

### Domain Layer Patterns
- Use sealed classes with private constructors for all aggregates
- Validation must occur in constructor; throw exceptions for invalid state
- Example: [User.cs](src/Farol.Domain/Users/User.cs) - email normalization and uniqueness checks

### Infrastructure Layer
- EF configurations in Persistence/Configurations/ follow the UserConfiguration.cs template
- Use Fluent API for table mappings, indexes, and constraints
- Direct DbContext injection in controllers (no Repository pattern)

### API Layer
- Controllers inherit from ControllerBase and are sealed
- Use [AuthenticatedUser](src/Farol.Api/Common/AuthenticatedUser.cs) for JWT claims
- Error responses: always return { "message": "..." }

### Testing
- Integration tests use FarolApiFactory with InMemory DB
- Test time fixed to 2026-03-10 12:00 UTC
- Run tests with: `dotnet test tests/Farol.Tests/Farol.Tests.csproj --no-build -c Release -m:1 -v minimal`

### Common Patterns
- Build with `-m:1 -v minimal` for reproducibility
- Migrations: run `dotnet-ef database update` manually
- Email uniqueness is case-insensitive (normalized in User.ctor)