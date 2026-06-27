# O Imperador Barber Shop — Frontend

## Tech Stack
- **Next.js 15** (App Router) + TypeScript
- **Tailwind CSS v4** — brand tokens defined in `src/app/globals.css` via `@theme`
- **TanStack Query v5** — server state, optimistic updates on barber dashboard
- **React Hook Form + Zod** — all forms with client + server validation
- **Axios** — HTTP client with Bearer token interceptor + 401 auto-refresh
- **MSW v2** — API mocking for unit tests
- **Vitest + React Testing Library** — unit/component tests
- **Playwright** — E2E tests

## Brand Colors
```
brand-gold:        #C9A84C
brand-gold-light:  #E8C96A
brand-gold-dark:   #A8872E
brand-black:       #0D0D0D   (background)
brand-black-soft:  #1A1A1A   (cards, inputs)
brand-white:       #F5F5F5   (text)
```
Fonts: Montserrat (headings), Inter (body)

## Route Structure
```
/                         Landing page (public)
/agendar                  Public 4-step booking wizard (no account needed)
/agendamento/[token]      Public appointment management (cancel / leave a review)
/login                    Barber login
/register/barber          Barber registration + availability picker
/barber/dashboard         Barber appointment management
```

## Auth Strategy
- Authentication exists for **barbers only** — clients never create an account.
- **Access token**: in-memory only (React context via AuthProvider)
- **Refresh token**: localStorage key `imperador_refresh_token`
- **userId**: localStorage key `imperador_user_id`
- **Route protection**: Next.js middleware reads `imperador_access_role` cookie, protects `/barber/*` only
- **Cookie**: set by AuthProvider after login, deleted on logout
- **Auto-refresh**: Axios 401 interceptor calls `/auth/refresh`, retries original request once
- **Session restore**: AuthProvider on mount reads localStorage, calls refresh endpoint

## Test Commands
```bash
npm test               # Run all unit tests
npm run test:watch     # Watch mode
npm run test:coverage  # With coverage report
npm run test:e2e       # Playwright E2E (requires dev server)
npm run test:e2e:ui    # Playwright UI mode
```

## Key Patterns
- All UI text in Brazilian Portuguese
- Date format: DD/MM/YYYY, currency: R$ X,XX
- Components: `src/components/{ui,auth,booking,appointments,layout}/`
- Hooks: `src/hooks/` — wrap TanStack Query + API calls
- API layer: `src/lib/api/` — typed Axios calls
- Types: `src/types/api.types.ts` — mirrors backend DTOs exactly
