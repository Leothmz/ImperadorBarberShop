# O Imperador Barber Shop

Barbershop scheduling platform. Clients book appointments anonymously (no account); barbers manage their agenda.

## Monorepo Structure

```
ImperadorBarberShop/
├── CLAUDE.md                  ← you are here (authoritative domain + API docs)
├── .gitignore
├── backend/                   ← ASP.NET Core 9, Clean Architecture
│   ├── CLAUDE.md
│   ├── ImperadorBarberShop.sln
│   ├── src/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   └── Api/
│   └── tests/
│       ├── UnitTests/
│       └── IntegrationTests/
└── frontend/                  ← Next.js 15, TypeScript, App Router
    ├── CLAUDE.md
    └── src/
```

---

## Brand

| Token | Value |
|-------|-------|
| Gold | `#C9A84C` |
| Gold Light | `#E8C96A` |
| Gold Dark | `#A8872E` |
| Black | `#0D0D0D` |
| Black Soft | `#1A1A1A` |
| White | `#F5F5F5` |

Fonts: **Montserrat** (headings), **Inter** (body).

---

## Domain Concepts (authoritative)

### Entities

| Entity | Key Fields |
|--------|-----------|
| `User` | Id (Guid), Name, Email, PasswordHash, Role (Barber), CreatedAt — clients are not `User`s; they identify themselves per-booking via name+phone |
| `Barber` | Id (Guid), UserId → User, Availability[], AverageRating (decimal) |
| `BarberAvailability` | Id, BarberId, DayOfWeek (0=Sun…6=Sat), StartTime (TimeOnly), EndTime (TimeOnly) |
| `Service` | Id, Name, Description, DurationMinutes (int), Price (decimal), IsActive |
| `Appointment` | Id, ClientName, ClientPhone, AccessToken (unique, opaque — powers the public manage/cancel/review link), BarberId → Barber, ScheduledAt (DateTime), TotalDurationMinutes, Status, Notes? |
| `AppointmentService` | AppointmentId, ServiceId (join table, M:N) |
| `Review` | Id, AppointmentId, BarberId, Rating (1–5 int), Comment (string?), CreatedAt |
| `RefreshToken` | Id, UserId, TokenHash (BCrypt hashed), ExpiresAt, IsRevoked |

### Enums

```csharp
public enum UserRole          { Client = 0, Barber = 1 }
public enum AppointmentStatus { Accepted = 0, Cancelled = 1, Completed = 2 }
```

`UserRole.Client` is unreachable via any public API — there is no client registration/login endpoint. It still exists in the enum for technical reasons only: the EF migration's backfill (`DELETE FROM "Users" WHERE "Role" = 0`) depends on the numeric value, and some internal test fixtures (`WebAppFixture`, auth tests) use `User.CreateClient(...)` as a generic test double. The Task 5 migration's backfill deletes every `Client`-role row, so in practice no `Client`-role `User` rows exist in the running system.

### Business Rules

- **Total duration** of an appointment = **sum** of `DurationMinutes` of all selected services.
- Clients book **without an account** — name + WhatsApp phone + barber + service(s) + slot only. Appointments are created already `Accepted` (no manual barber approval step).
- Each appointment gets a unique `AccessToken` at creation, used for the public "manage appointment" link (cancel, and later — once `Completed` — leave a review). This is the only way a client identifies their own appointment.
- A client can submit a `Review` (via the access-token link) for an appointment where `Status == Completed`.
- A client can cancel an appointment (via the access-token link) if it is `Accepted` AND `ScheduledAt > UtcNow + 2 hours`.
- A barber can cancel a confirmed appointment directly (e.g. emergencies) via `PATCH /appointments/{id}/cancel-by-barber`.
- A barber cannot have two `Accepted` appointments that overlap in time.
- `BarberAvailability` constraint: unique per `(BarberId, DayOfWeek)`; `StartTime < EndTime`.
- Unique DB constraint on `(BarberId, ScheduledAt)` prevents double-booking race conditions.
- Anti-spam on appointment creation: rate-limited per IP (5/hour, HTTP layer) and per `ClientPhone` (3/hour, application layer).

### The access-token link is the client's only handle

There is no client account, so the token URL is the whole relationship. The UI treats it
as such:

- Booking redirects to `/agendamento/{token}?novo=1`. That flag turns the manage page into
  a confirmation — gold check, "Agendamento confirmado", and an explicit *"Guarde este
  link"* — instead of the same card a three-week-old link would open.
- The page offers **Guardar link** (`navigator.share`, falling back to clipboard) and
  **Adicionar à agenda** (`.ics` built client-side in floating local time, so no timezone
  conversion moves the appointment).
- The 2-hour cancellation window is disclosed *before* booking, on the confirmation step —
  it used to surface only as a dead button at the moment the client needed to cancel.

The "appointment created" notification still goes to the **barber**, not the client. Until
that changes, the browser the client booked from is the only place the link exists.

---

## Service Catalog (global, seeded)

| Name (PT) | Name (EN) | Duration (min) | Price (BRL) |
|-----------|-----------|---------------|-------------|
| Corte | Haircut | 30 | 35.00 |
| Fade / Disfarçado | Fade | 40 | 45.00 |
| Barba | Beard | 20 | 25.00 |
| Sobrancelha | Eyebrows | 15 | 15.00 |
| Hidratação | Hydration | 20 | 30.00 |
| Pigmentação | Pigmentation | 30 | 40.00 |

---

## API Contract

**Base URL (local):** `http://localhost:5000/api/v1`  
**Auth:** `Authorization: Bearer <access_token>` (JWT, barber only)  
**Roles in JWT claim `role`:** `Barber` only

### Auth (public)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/auth/register/barber` | Register new barber (payload includes availability) |
| POST | `/auth/login` | Login (barber only) → returns `{ accessToken, refreshToken, role, userId, barberId }` |
| POST | `/auth/refresh` | Exchange refresh token → new token pair |

### Services (public)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/services` | List all active services |

### Barbers

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/barbers` | Public | List all barbers (id, name, avatarUrl, averageRating) |
| GET | `/barbers/{id}` | Public | Barber profile + availability + averageRating |
| GET | `/barbers/{id}/reviews` | Public | Paginated reviews for a barber |
| GET | `/barbers/{id}/slots?date=YYYY-MM-DD&serviceIds=id1,id2` | Public | Available booking slots |
| PUT | `/barbers/me/availability` | Barber | Update own availability windows |

### Appointments

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/appointments` | Public (rate-limited) | Create appointment — `clientName, clientPhone, barberId, scheduledAt, serviceIds, notes?`. Auto-confirmed (`Accepted`). Returns `{ id, accessToken }`. Triggers email to barber. |
| GET | `/appointments/manage/{token}` | Public | Appointment status/details for the public manage page |
| POST | `/appointments/manage/{token}/cancel` | Public | Client cancels via their access token (>2h before, `Accepted` only) — the UI confirms in a modal first |
| POST | `/appointments/manage/{token}/review` | Public | Client submits a review via their access token (only if `Completed`) |
| GET | `/appointments/barber` | Barber | All appointments for logged-in barber |
| PATCH | `/appointments/{id}/cancel-by-barber` | Barber | Barber-initiated cancel (e.g. emergencies) |
| PATCH | `/appointments/{id}/complete` | Barber | Mark as Completed → unlocks the client's review link |
| PATCH | `/appointments/{id}/payment` | Barber | Register/update payment method |

### Admin — appointments

The admin manages **any** barber's appointments. The commands take a nullable
`RequesterBarberId`: the barber-facing endpoints pass the JWT's `barberId` (IDOR check
applies), the admin endpoints pass `null` (check skipped). Same command, same rules,
different caller.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/admin/barbers/{id}/appointments` | Every appointment of that barber, any status |
| PATCH | `/admin/appointments/{id}/complete` | Mark as Completed — optional body `{ paymentMethod? }` |
| PATCH | `/admin/appointments/{id}/cancel` | Cancel the appointment |
| PATCH | `/admin/appointments/{id}/payment` | Register/update payment method |

### Reviews

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/barbers/{id}/reviews` | Public | List reviews — submission happens via `/appointments/manage/{token}/review` above |

---

## Notifications

Channels are configured at runtime in `AppSettings` under `notifications:channels`
(`email`, `whatsapp` or `email,whatsapp`), editable at `/admin/whatsapp` → Notificações.

| Event | Recipient | Channel |
|-------|-----------|---------|
| Appointment created | Barber | per `notifications:channels` |
| Appointment cancelled | Client | per `notifications:channels` |
| Appointment completed | Client | per `notifications:channels` |
| Reminder before appointment | Client | per `notifications:channels` |

**Sending is asynchronous.** Handlers call `INotificationQueue.Enqueue(...)` and return
immediately; `NotificationDispatcher` (a `BackgroundService`) drains the queue, each job in
its own DI scope. Never `await` `INotificationService` inside a handler — a slow SMTP or
WhatsApp gateway would hold the client's HTTP response until the network timeout.

The queue lives in memory: pending notifications are lost if the process dies. Acceptable
for best-effort delivery; persist it if delivery ever becomes a hard requirement.

---

## Deploy Configuration

All config comes from environment variables — see [`.env.example`](.env.example) for the
full list with explanations. ASP.NET maps `__` to section nesting (`Jwt__Secret` →
section `Jwt`, key `Secret`).

Two different behaviours on startup, by design:

| Keys | Behaviour | Why |
|------|-----------|-----|
| `WHATSAPP__EVOLUTIONAPIURL`, `WHATSAPP__EVOLUTIONAPIKEY`, `WHATSAPP__INSTANCENAME` | Env var wins on **every** boot | Infrastructure, not editable in the admin UI — rotating a key or moving the server must take effect on restart |
| `NOTIFICATIONS__CHANNELS` | Seeds only when the key is absent | Editable at `/admin/whatsapp`; overwriting on each boot would undo the admin's choice |

First boot also creates the admin user from `Admin__Email` / `Admin__Password`. Changing
those later does **not** change an existing admin's password.

### WhatsApp (Evolution API)

Evolution API is a separate service — the app only talks to it over HTTP. Bring it up with
[`docker-compose.evolution.yml`](docker-compose.evolution.yml), point
`WHATSAPP__EVOLUTIONAPIURL` at it, reuse the same API key on both sides, then pair the
barbershop's phone by scanning the QR code at `/admin/whatsapp`.

Keep port 8080 off the public internet: anyone holding the API key can send messages as
that number. The compose file binds it to `127.0.0.1` for this reason.

Evolution API drives WhatsApp Web with a real number (unofficial). Fine for this volume,
but don't use a personal number — Meta can ban it. The official path is WhatsApp Cloud API.

### Persistent volumes

Two things must survive a redeploy, or data is silently lost:
- the SQLite file in `ConnectionStrings__DefaultConnection`
- the `evolution_instances` volume (otherwise the WhatsApp session drops and the QR must be scanned again)

---

## Local Development

### Prerequisites
- .NET SDK 9
- Node.js 24+

The database is SQLite — a local `imperador_barber.db` file created and migrated automatically on first run in Development. No Docker, no external database server.

### Start Backend
```bash
cd backend
dotnet run --project src/Api/ImperadorBarberShop.Api
# API available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Start Frontend
```bash
cd frontend
npm run dev
# App available at http://localhost:3000
```

### Run All Tests
```bash
# Backend
cd backend && dotnet test --collect:"XPlat Code Coverage"

# Frontend unit/component
cd frontend && npm test

# Frontend E2E
cd frontend && npx playwright test
```

---

## Environment Variables

### Backend (`backend/src/Api/appsettings.Development.json` — gitignored)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=imperador_barber.db"
  },
  "Jwt": {
    "SecretKey": "<min-256-bit-random-string>",
    "Issuer": "ImperadorBarberShop",
    "Audience": "ImperadorBarberShopFrontend",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "Email": {
    "SmtpHost": "smtp.mailtrap.io",
    "SmtpPort": 587,
    "Username": "<mailtrap-user>",
    "Password": "<mailtrap-pass>",
    "FromAddress": "noreply@imperadorbarber.com",
    "FromName": "O Imperador Barber Shop"
  },
  "FrontendUrl": "http://localhost:3000"
}
```

### Frontend (`.env.local` — gitignored)
```
NEXT_PUBLIC_API_URL=http://localhost:5000
# Opcional. Só dígitos, formato internacional. Sem ele a UI esconde o contato.
NEXT_PUBLIC_WHATSAPP_NUMBER=5511999990000
```

`NEXT_PUBLIC_WHATSAPP_NUMBER` is the client's escape hatch: it renders the "falar com a
barbearia" link when a booking is rate-limited (429) and when someone needs to cancel
inside the final 2 hours, which the site itself refuses. Unset, those links simply do not
render — no invented phone number.
