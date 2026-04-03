---
description: "Use when: working with Next.js frontend code, components, pages, or API client. Provides guidance on App Router, auth, and TypeScript patterns."
applyTo: ["web/**/*.{ts,tsx,js,jsx}"]
---

## Next.js Frontend Instructions

### App Router Structure
- Routes mirror domain boundaries: /dashboard, /bills, /budget, etc.
- Use feature-driven organization

### Authentication
- Client-side with localStorage (temporary MVP design)
- Not persistent on page reload; no refresh tokens
- API calls use centralized client in [lib/api.ts](web/lib/api.ts)

### TypeScript
- Strict mode enabled; no implicit any
- All API types defined in api.ts

### Testing
- Use Vitest: `npm run test`
- Component tests in .test.tsx files

### Common Patterns
- CORS hardcoded to localhost:3000/3001
- NEXT_PUBLIC_API_BASE_URL for API endpoint