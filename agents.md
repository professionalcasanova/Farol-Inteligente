# AGENTS.md

## Product context
This repository contains an AI-powered personal finance assistant for Brazilian users.
The product helps users organize expenses, track bills, avoid late payments, and receive financial guidance.

## Engineering principles
- Prefer simple and modular architecture
- Keep backend domain logic isolated
- Avoid premature optimization
- Write readable code with explicit naming
- Add tests for business rules
- Document assumptions briefly in markdown when needed

## Product priorities
- Fast MVP delivery
- Clear demo value
- Real user pain first
- Brazilian financial context
- Privacy and consent by default

## Current MVP scope
- Manual/CSV input of transactions
- Expense categorization
- Monthly summary
- Bill due-date tracking
- Alerts
- Natural language finance assistant

## Out of scope for now
- Real bank integrations
- Automatic payments
- Debt negotiation with third parties
- Credit scoring
- Complex investment features

## Response style
When proposing changes:
1. Explain the plan
2. List touched files
3. Implement incrementally
4. Include validation steps
5. Highlight tradeoffs

## Testing principles
- Every relevant feature must include automated tests
- Prefer unit tests first, then integration tests when the flow becomes stable
- Keep tests simple, readable, and fast
- Avoid unnecessary test infrastructure in early stages
- A feature is not considered complete unless build and tests pass

## Definition of done
For each completed step:
1. Build passes
2. Automated tests pass
3. Manual validation steps are documented when needed
4. Key tradeoffs are documented briefly

## Version control rules

The agent must use git commits to keep track of changes.

Rules:

- Every meaningful step must be committed
- Commit messages must follow conventional commits
- Never commit broken builds
- Tests must pass before committing
- Every pull request must include closing keywords for its issues in the body, for example `Closes #123`
- When a pull request covers more than one issue, include one closing line per issue

Before committing:
1. Run build
2. Run tests
3. Confirm success
4. Then commit

## Architecture Overview

This is a multi-stack personal finance assistant with the following components:

- **Backend**: .NET 10 modular monolith with sealed domain classes. No Repository pattern, no Application layer, no CQRS (MVP constraint). Domain entities use private constructors with validation. Layers: Domain (aggregates), Infrastructure (Persistence + Auth), API (Controllers direct to DbContext).

- **Frontend**: Next.js 15 with App Router, feature-driven routing. Client-side auth with localStorage (temporary for MVP).

- **Intelligence Service**: Python FastAPI service with single endpoint for deterministic financial insights.

- **Database**: PostgreSQL 16.8 via Docker Compose.

Bounded contexts: Users, Bills, Categories, Budgets, Ledger (Accounts & Transactions).

## Build and Test Commands

**Backend (.NET)**:
- Restore: `dotnet restore Farol.sln`
- Build: `dotnet build Farol.sln --no-restore -c Release -m:1 -v minimal`
- Migrate DB: `dotnet-ef database update --project src/Farol.Infrastructure/Farol.Infrastructure.csproj --startup-project src/Farol.Api --context FarolDbContext --no-build`
- Tests: `dotnet test tests/Farol.Tests/Farol.Tests.csproj --no-build -c Release -m:1 -v minimal`
- Run: `dotnet run --project src/Farol.Api/Farol.Api.csproj -c Release --no-build`

**Frontend (Next.js)**:
- Install: `cd web && npm install`
- Dev: `npm run dev` (port 3000)
- Tests: `npm run test` (Vitest)

**Python Service**:
- Install: `cd services/farol_intelligence && pip install -e .`
- Run: `uvicorn app.main:app --reload` (port 8000)

**Infrastructure**:
- DB: `docker-compose up -d`

## Key Conventions

- **Domain Immutability**: Private constructors with validation methods (e.g., [User.cs](src/Farol.Domain/Users/User.cs)).
- **Build Flags**: Use `-m:1 -v minimal` for reproducible builds.
- **Test Time**: Fixed to 2026-03-10 12:00 UTC in [FarolApiFactory.cs](tests/Farol.Tests/Api/FarolApiFactory.cs).
- **Error Responses**: Always `{ "message": "..." }` format.
- **Sealed Classes**: Domain entities, Controllers, Services to prevent accidental inheritance.
- **Database**: InMemory for tests, PostgreSQL for dev/prod.
- **Frontend Auth**: Temporary localStorage (no refresh tokens).

## Common Pitfalls

- **Environment Setup**: Set `$env:DOTNET_CLI_HOME='c:\Users\masuc\Desktop\PensarNoNome\.dotnet'` and `$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'` before first build.
- **Auth Persistence**: localStorage auth not persistent on page reload (MVP design).
- **CORS**: Hardcoded to localhost:3000/3001 only.
- **Email Uniqueness**: Case-insensitive, normalized in User constructor.
- **Migrations**: Not auto-applied; run `dotnet-ef database update` manually.
- **TypeScript**: Strict mode enabled; implicit `any` fails build.
- **Python Integration**: Called from Insights endpoints; no visible retry logic.

## Key Files and Patterns

- [User.cs](src/Farol.Domain/Users/User.cs): Sealed aggregate with validation.
- [UserConfiguration.cs](src/Farol.Infrastructure/Persistence/Configurations/UserConfiguration.cs): EF Fluent API template.
- [AccountsController.cs](src/Farol.Api/Modules/Accounts/AccountsController.cs): Secured HTTP handler pattern.
- [AuthenticatedUser.cs](src/Farol.Api/Common/AuthenticatedUser.cs): JWT claim extraction utility.
- [FarolApiFactory.cs](tests/Farol.Tests/Api/FarolApiFactory.cs): Integration test fixture.
- [api.ts](web/lib/api.ts): Frontend API contract.
- [analysis.py](services/farol_intelligence/app/analysis.py): Insight rule engine.

## Documentation Links

- [README.md](README.md): Setup and endpoint summaries.
- [docs/demo-scenarios.md](docs/demo-scenarios.md): Seed users for testing.
- [docs/sprints/](docs/sprints/): Sprint notes with architecture decisions (sprint-1.md to sprint-10.md).
