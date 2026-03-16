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

Before committing:
1. Run build
2. Run tests
3. Confirm success
4. Then commit