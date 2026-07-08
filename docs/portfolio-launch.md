# Portfolio launch checklist

## GitHub presentation

Recommended repository name: `farol` or `farol-finance`.

Recommended description:

> Open-source personal finance platform built with Next.js, ASP.NET Core, PostgreSQL, and FastAPI, featuring secure authentication and deterministic financial insights.

Recommended topics:

`personal-finance`, `dotnet`, `aspnet-core`, `nextjs`, `react`, `typescript`, `postgresql`, `fastapi`, `layered-architecture`, `docker`

Before changing visibility to public:

- Merge the reviewed `dev` state into `master`.
- Keep `master` as the default and release-ready branch.
- Retain `dev` only if it remains useful and protected; every branch is visible in a public repository.
- Delete merged feature, experiment, and abandoned remote branches.
- Enable branch protection and required CI checks on `master` and `dev`.
- Enable private vulnerability reporting, Dependabot alerts, and secret scanning.
- Add a repository social-preview image and at least three screenshots to the README.
- Create a tagged release after the public baseline is stable.
- Verify the live demo uses synthetic data and isolated credentials.

## LinkedIn project description

### Project entry

**Farol — Open-source personal finance platform**

Designed and built a full-stack financial management platform using Next.js, React, TypeScript, ASP.NET Core, PostgreSQL, Entity Framework Core, and FastAPI. Implemented secure authentication with token rotation and session management, user-scoped authorization, recurring bills, budgets, CSV imports, and deterministic financial insights. Structured the system as independently buildable web, API, persistence, and analysis components with automated CI and security checks.

### Suggested launch post

> I am publishing Farol, an open-source personal finance platform and one of my main software engineering portfolio projects.
>
> Farol combines a Next.js and TypeScript frontend, an ASP.NET Core API, PostgreSQL persistence, and an isolated FastAPI service for deterministic financial analysis.
>
> The project includes secure session management, user-scoped authorization, monthly budgets, recurring bills, CSV imports, automated tests, CI pipelines, dependency scanning, and production-oriented configuration.
>
> I built it to solve a practical financial organization problem while demonstrating how I approach architecture, security, testing, and cross-stack delivery.
>
> Repository: [add GitHub URL]
> Live demo: [add demo URL]
>
> I am open to international software engineering opportunities involving .NET, TypeScript, cloud-native systems, and product-focused backend development.

Use one strong product screenshot or a short demo video. Avoid posts containing test credentials, infrastructure dashboards, database consoles, environment variables, or real financial records.
