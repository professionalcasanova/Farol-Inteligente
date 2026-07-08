# Contributing to Farol

Thank you for considering a contribution.

## Development workflow

1. Create a focused branch from `dev`.
2. Keep changes within the relevant component boundary.
3. Add or update tests for behavioral changes.
4. Run the checks for every affected component.
5. Open a pull request into `dev` with the problem, approach, and verification steps.

Release-ready changes are promoted from `dev` to `master`. Direct commits to `master` are discouraged.

## Required checks

Backend:

```bash
dotnet test Farol.sln
```

Frontend:

```bash
cd web
npm ci
npm run lint
npm run test
npm run build
```

Intelligence service:

```bash
cd services/farol_intelligence
python -m unittest discover tests
```

## Security and data handling

- Never commit credentials, tokens, private keys, production URLs containing credentials, database dumps, or personal financial data.
- Use synthetic records in tests, documentation, screenshots, and demos.
- Report vulnerabilities according to [SECURITY.md](SECURITY.md), not through a public issue.

## Pull requests

Keep pull requests small enough to review. Explain any architectural trade-off and call out schema, configuration, or deployment changes explicitly.
