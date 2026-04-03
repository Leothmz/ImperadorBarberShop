# O Imperador Barber Shop

Barbershop scheduling platform. Clients book appointments; barbers manage their agenda, accept/reject bookings.

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
| `User` | Id (Guid), Name, Email, PasswordHash, Role (Client\|Barber), CreatedAt |
| `Barber` | Id (Guid), UserId → User, Availability[], AverageRating (decimal) |
| `BarberAvailability` | Id, BarberId, DayOfWeek (0=Sun…6=Sat), StartTime (TimeOnly), EndTime (TimeOnly) |
| `Service` | Id, Name, Description, DurationMinutes (int), Price (decimal), IsActive |
| `Appointment` | Id, ClientId → User, BarberId → Barber, ScheduledAt (DateTime), TotalDurationMinutes, Status, Notes? |
| `AppointmentService` | AppointmentId, ServiceId (join table, M:N) |
| `Review` | Id, AppointmentId, ClientId, BarberId, Rating (1–5 int), Comment (string?), CreatedAt |
| `RefreshToken` | Id, UserId, TokenHash (BCrypt hashed), ExpiresAt, IsRevoked |

### Enums

```csharp
public enum UserRole        { Client = 0, Barber = 1 }
public enum AppointmentStatus { Pending = 0, Accepted = 1, Rejected = 2, Cancelled = 3, Completed = 4 }
```

### Business Rules

- **Total duration** of an appointment = **sum** of `DurationMinutes` of all selected services.
- A client can only submit a `Review` for an appointment where `Status == Completed`.
- A client can cancel an appointment if it is `Pending` or `Accepted` AND `ScheduledAt > UtcNow + 2 hours`.
- A barber cannot have two `Accepted` appointments that overlap in time.
- `BarberAvailability` constraint: unique per `(BarberId, DayOfWeek)`; `StartTime < EndTime`.
- Unique DB constraint on `(BarberId, ScheduledAt)` prevents double-booking race conditions.

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
**Auth:** `Authorization: Bearer <access_token>` (JWT)  
**Roles in JWT claim `role`:** `Client` | `Barber`

### Auth (public)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/auth/register/client` | Register new client |
| POST | `/auth/register/barber` | Register new barber (payload includes availability) |
| POST | `/auth/login` | Login → returns `{ accessToken, refreshToken, role, userId }` |
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
| GET | `/barbers/{id}/slots?date=YYYY-MM-DD&serviceIds=id1,id2` | Client | Available booking slots |
| PUT | `/barbers/me/availability` | Barber | Update own availability windows |

### Appointments

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/appointments` | Client | Create appointment (triggers email to barber) |
| GET | `/appointments/mine` | Client | Client's own appointments |
| DELETE | `/appointments/{id}` | Client | Cancel (>2h before, Pending or Accepted only) |
| GET | `/appointments/barber` | Barber | All appointments for logged-in barber |
| PATCH | `/appointments/{id}/accept` | Barber | Accept → triggers email to client |
| PATCH | `/appointments/{id}/reject` | Barber | Reject → triggers email to client |
| PATCH | `/appointments/{id}/complete` | Barber | Mark as Completed → unlocks client review |

### Reviews

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/reviews` | Client | Submit review (only for Completed appointments) |
| GET | `/barbers/{id}/reviews` | Public | List reviews |

---

## Email Notifications (via IEmailService)

| Event | Recipients | Subject |
|-------|-----------|---------|
| Appointment created | Barber | "Novo agendamento de {clientName}" |
| Appointment accepted | Client | "Seu agendamento foi aceito!" |
| Appointment rejected | Client | "Seu agendamento foi recusado" |

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
