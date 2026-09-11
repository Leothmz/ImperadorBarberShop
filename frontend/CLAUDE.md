# O Imperador Barber Shop — Frontend

## Tech Stack
- **Next.js 16** (App Router) + TypeScript
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
                          `?novo=1` renders it as the post-booking confirmation
/login                    Staff login (Barber and Admin)
/register/barber          Legacy path — `redirect('/login')`, no registration UI
/barber/dashboard         Barber agenda + blocks (tabs)
/admin/dashboard          Financial dashboard + clients to invite back (recurrence)
/admin/barbers            Barber CRUD, availability and blocks
/admin/services           Service catalog CRUD
/admin/whatsapp           WhatsApp connection + notification settings
```

## Auth Strategy
- Authentication exists for **staff only** (Barber and Admin) — clients never create an account.
- **Access token**: in-memory only (`lib/api/client.ts`); lost on reload and recovered via refresh.
- **Refresh token**: an `HttpOnly` cookie named `imperador_refresh_token`, set by the API on
  `/auth/login` and `/auth/refresh`. Page JavaScript never reads it.
- **userId**: localStorage key `imperador_user_id` — only a hint that a session may exist, and the
  body of the refresh call; it grants no privilege.
- **Route protection**: `src/proxy.ts` (the Next 16 rename of `middleware.ts`) gates `/admin/*` and
  `/barber/*` on the presence of the API-issued cookie; the `admin/layout.tsx` and `barber/layout.tsx`
  then check the role held by `AuthProvider` (which only ever comes from an API response).
- **Same-origin auth calls**: login/refresh/logout go through `authClient` on `/api/v1/auth`, rewritten
  to the API in `next.config.ts`, so the cookie is stored on this host where `proxy.ts` sees it.
- **Auto-refresh**: the Axios 401 interceptor calls `/auth/refresh` once, queues concurrent requests,
  and retries; on failure it clears storage and dispatches `auth:logout`.
- **Session restore**: `AuthProvider` on mount reads the stored userId, calls refresh, and restores the
  access token; anonymous visitors never fire a request.

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

## Client-journey utilities

| Module | Why it exists |
|--------|---------------|
| `lib/utils/appointmentError.ts` | Maps a failed booking to the *right* recovery. A 409 sends the client back to the slot grid; a 429 never says "tente novamente" (that guarantees a second failure) and offers the WhatsApp link instead. |
| `lib/utils/bookingDraft.ts` | Mirrors the wizard into `sessionStorage`. The audience switches apps mid-flow to copy a phone number; without this, coming back restarts at step 1. `clampStep` refuses to restore past the data actually saved. |
| `lib/utils/ics.ts` | Builds the calendar file in floating local time — the API works in wall-clock time and a UTC conversion would move the appointment. |
| `lib/utils/whatsapp.ts` | Returns `null` when `NEXT_PUBLIC_WHATSAPP_NUMBER` is unset, so contact links disappear rather than pointing nowhere. |

`buttonClasses()` (exported from `components/ui/Button.tsx`) styles a `<Link>` as a button.
Use it instead of wrapping `<Button>` in `<Link>` — that renders `<a><button>`, which is
invalid interactive nesting and costs the link its screen-reader semantics.

The wizard keeps its step in `history.state` (`?passo=N`), so the browser Back button walks
back one step instead of leaving the booking entirely.

## Landing page

`src/components/landing/` — the landing is composed of client sections that read real data;
it never hardcodes a service, a price or a rating.

| Component | Notes |
|---|---|
| `HairRain` | Canvas hero background. Listens for the `imperador:snip` window event to release a burst of strands at a point. Pauses offscreen/hidden, static frame under `prefers-reduced-motion`. |
| `SnipButton` | The hero CTA. Tracks the pointer into `--mx`/`--my` for the `.snip-cta` highlight and dispatches `imperador:snip` on click. |
| `HowItWorks` | Static, factual. The step numbers stay because the order is the information. |
| `ServiceBoard` | The price board, from `useServices()`. Staggered reveal on first scroll into view. |
| `BarberLineup` | Real barbers from `useBarbers()`, each deep-linking to `/agendar?barbeiro=<id>`. Renders nothing when the list is empty or errored rather than showing a decorated void. |

`/agendar` reads `?barbeiro=<id>` and jumps straight to step 2 with that barber selected.

Landing copy is bound by PRODUCT.md: no tenure claims, no testimonials, no ratings the page
does not actually display. `tests/unit/app/HomePage.test.tsx` asserts those absences.
