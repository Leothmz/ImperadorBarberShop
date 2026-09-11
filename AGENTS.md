# O Imperador Barber Shop

Barbershop scheduling platform. Clients book appointments anonymously (no account); barbers and
admins sign in with email + password.

## Where things live

```
ImperadorBarberShop/
├── AGENTS.md            ← this file (CLAUDE.md is a symlink to it)
├── README.md            ← contributor setup recipe + user-facing API reference
├── .env.example         ← authoritative environment-variable list + startup semantics
├── PRODUCT.md           ← product intent, audience, constraints
├── DESIGN.md            ← design system
├── backend/             ← ASP.NET Core 9, Clean Architecture (backend/CLAUDE.md)
└── frontend/            ← Next.js 16, App Router (frontend/CLAUDE.md)
```

Authoritative sources — prefer them over copying detail here:

- Running the stack: `README.md` → "Como Rodar Localmente".
- Config keys and boot behaviour: `.env.example`.
- Backend structure, tests, migrations: `backend/CLAUDE.md`.
- Frontend routes, state, auth: `frontend/CLAUDE.md`.
- HTTP surface: the controllers in `backend/src/Api/ImperadorBarberShop.Api/Controllers/`.
- Seeded service catalog: `backend/src/Infrastructure/.../Persistence/Configurations/ServiceConfiguration.cs`.

## Brand

Gold `#C9A84C` / light `#E8C96A` / dark `#A8872E`; black `#0D0D0D` / soft `#1A1A1A`; white `#F5F5F5`.
Fonts Montserrat (headings) + Inter (body). Tokens are defined once in `frontend/src/app/globals.css`.

## Domain model

Entities: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/`.

- `User` — Id, Name, Email, PasswordHash, Role, CreatedAt. Staff only; clients are never `User`s.
- `Barber` — Id, UserId, AverageRating, IsActive, PhotoUrl.
- `BarberAvailability` — Id, BarberId, DayOfWeek, StartTime/EndTime (`TimeOnly`). Unique
  `(BarberId, DayOfWeek)`: one window per day, so a lunch gap needs a `BarberBlock`.
- `BarberBlock` — Id, BarberId, StartsAt, EndsAt, Description?, IsRecurring, RecurrenceDays?
  (bitmask), RecurrenceEndsAt?.
- `Service` — Id, Name, Description, DurationMinutes, Price, IsActive, PhotoUrl. One global catalog.
- `ServiceAddon` — ParentServiceId, AddonServiceId (self-join).
- `Appointment` — Id, ClientName, ClientPhone, AccessToken (unique), BarberId, ScheduledAt,
  TotalDurationMinutes, Status, Notes?, CreatedAt, UpdatedAt, ReminderSentAt?, PaymentMethod?, PaidAt?.
- `AppointmentService` — AppointmentId, ServiceId, `UnitPrice` (price snapshot at booking; financial
  reports read this, never the live price).
- `Review` — Id, AppointmentId, BarberId, Rating (1–5), Comment?, CreatedAt.
- `Expense` — Id, Amount, Description, Date, CreatedAt, CreatedByUserId.
- `AppSettings` — Key/Value runtime settings (`notifications:*`, `whatsapp:*`).
- `RefreshToken` — Id, UserId, TokenHash (BCrypt), ExpiresAt, IsRevoked.

Enums (`backend/src/Domain/.../Enums/`):

```csharp
public enum UserRole          { Client = 0, Barber = 1, Admin = 2 }
public enum AppointmentStatus { Accepted = 0, Cancelled = 1, Completed = 2 }
public enum PaymentMethod     { Dinheiro = 0, Cartão = 1, Pix = 2 }
```

`UserRole.Client` is reachable through no endpoint — there is no client registration or login. It
survives because integration fixtures use `User.CreateClient(...)` as a test double.

## Business rules

- Appointment duration = the sum of the selected services' `DurationMinutes`.
- Clients book without an account: name + WhatsApp phone + barber + services + slot. Appointments are
  created already `Accepted` (no approval step).
- Each appointment gets a unique `AccessToken`; the public `/agendamento/{token}` link is the client's
  only handle (view, cancel, review).
- A review requires `Status == Completed`; a client cancel requires `Accepted` AND more than 2h before
  `ScheduledAt`.
- A barber cannot hold two overlapping `Accepted` appointments; a unique `(BarberId, ScheduledAt)`
  index guards the race.
- Anti-spam on creation: 5/hour per IP (rate-limiter middleware) and 3/hour per `ClientPhone`
  (application handler).
- Two authorization policies (`Program.cs`): `RequireBarberRole`, `RequireAdminRole`. Admin commands
  take a nullable `RequesterBarberId`; barber endpoints pass the JWT's `barberId` (IDOR check), admin
  endpoints pass `null` (check skipped).

### Time is shop wall-clock

`ShopTimeProvider` (`backend/src/Infrastructure/.../Services/ShopTimeProvider.cs`) pins the shop zone
to `America/Sao_Paulo` regardless of host timezone. `ScheduledAt`, availability and blocks are stored
as **wall-clock, zone-less** values, and every "now" compared with them must come from
`TimeProvider.GetLocalNow().DateTime` — never `DateTime.UtcNow`, which on a UTC host is 3h ahead. The
frontend sends and reads naive strings for the same reason (the `.ics` file is built in floating local
time).

## Notifications

Channels live in `AppSettings["notifications:channels"]` (`email`, `whatsapp` or `email,whatsapp`),
editable at `/admin/whatsapp` → Notificações. Sends are fire-and-forget: handlers call
`INotificationQueue.Enqueue(...)`; `NotificationDispatcher` drains an in-memory queue in its own DI
scope. Pending notifications are lost on process death. Email delivery exists only for *appointment
created*; cancelled/completed/reminder honour `whatsapp` only (`NotificationService.cs`).

## Deploy config

All config comes from environment variables; `.env.example` is the authoritative list. Startup
semantics (`Program.cs`):

- `WHATSAPP__*` env vars overwrite `AppSettings` on every boot.
- `NOTIFICATIONS__CHANNELS` seeds only when the key is absent, so the admin UI choice survives restarts.
- `Admin__Email` / `Admin__Password` create the first admin once; changing them later does not rotate
  an existing password.
- Migrations, WAL, service seed and admin seed run on every boot, in all environments.
- Cloudinary is optional: without it the admin API runs and only photo uploads fail.

## Local development

See `README.md` → "Como Rodar Localmente". Short version: backend on `http://localhost:5044`
(`cd backend && dotnet run --project src/Api/ImperadorBarberShop.Api`), frontend on
`http://localhost:3000` (`cd frontend && npm run dev`) with `NEXT_PUBLIC_API_URL=http://localhost:5044`.

## Tests

CI (`.github/workflows/ci.yml`) runs three jobs on every PR: Backend, Frontend, E2E.

- Backend: `cd backend && dotnet test ImperadorBarberShop.sln`.
- Frontend unit/component: `cd frontend && npm test`.
- E2E (Playwright, chromium): `cd frontend && npx playwright test`. `playwright.config.ts` boots both
  servers itself — backend via `dotnet run` (temp SQLite at `/tmp/imperador-e2e-*.db`) and frontend via
  `npm run dev`; admin credentials come from `E2E_ADMIN_EMAIL`/`E2E_ADMIN_PASSWORD` (defaults in the
  config mirror CI). Specs live in `frontend/tests/e2e/`; `helpers.ts` seeds barbers through the admin
  API, the journeys themselves drive the real UI.

## Sharp edges

- `frontend/src/proxy.ts` (Next 16's rename of `middleware.ts`) gates `/admin/*` and `/barber/*` on the
  API-issued `imperador_refresh_token` `HttpOnly` cookie; the area layouts then enforce the role from
  `AuthProvider`.
- The refresh token never reaches JavaScript — only the access token (in memory) and `userId`
  (localStorage).
- `frontend/src/lib/api/client.ts` and the MSW handlers fall back to port `5000` when
  `NEXT_PUBLIC_API_URL` is unset, but the HTTP profile serves `5044`. Always set the env var.

## Maintaining this file

Keep this file for knowledge useful to almost every future agent session in this project.
Do not repeat what the codebase already shows; point to the authoritative file or command instead.
Prefer rewriting or pruning existing entries over appending new ones.
When updating this file, preserve this bar for all agents and keep entries concise.
