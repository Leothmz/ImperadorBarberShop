# O Imperador Barber Shop

Barbershop scheduling platform. Clients book appointments anonymously (no account); barbers manage their agenda.

## Monorepo Structure

```
ImperadorBarberShop/
├── CLAUDE.md                  ← you are here (authoritative domain + API docs)
├── .gitignore
├── docker-compose.yml         ← PostgreSQL 16 for local dev
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
| POST | `/appointments/manage/{token}/cancel` | Public | Client cancels via their access token (>2h before, `Accepted` only) |
| POST | `/appointments/manage/{token}/review` | Public | Client submits a review via their access token (only if `Completed`) |
| GET | `/appointments/barber` | Barber | All appointments for logged-in barber |
| PATCH | `/appointments/{id}/cancel-by-barber` | Barber | Barber-initiated cancel (e.g. emergencies) |
| PATCH | `/appointments/{id}/complete` | Barber | Mark as Completed → unlocks the client's review link |

### Reviews

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/barbers/{id}/reviews` | Public | List reviews — submission happens via `/appointments/manage/{token}/review` above |

---

## Email Notifications (via IEmailService)

| Event | Recipients | Subject |
|-------|-----------|---------|
| Appointment created | Barber | "Novo agendamento de {clientName}" |

---

## Local Development

### Prerequisites
- Docker Desktop (for PostgreSQL)
- .NET SDK 9
- Node.js 24+

### Start PostgreSQL
```bash
docker-compose up -d
```

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
    "DefaultConnection": "Host=localhost;Port=5432;Database=imperador_barber;Username=imperador;Password=localdev"
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
```
