# Backend — O Imperador Barber Shop

## Tech Stack
- .NET 9, ASP.NET Core, EF Core 9 + Npgsql (PostgreSQL 16)
- MediatR (CQRS), FluentValidation, AutoMapper
- BCrypt.Net-Next (password hashing, cost 12)
- JWT Bearer (access token 15 min, refresh token 7 days)
- MailKit (SMTP email)

## Solution Structure (Clean Architecture)

```
src/
├── Domain/ImperadorBarberShop.Domain/          ← Entities, Enums, Repository interfaces — NO external deps
├── Application/ImperadorBarberShop.Application/ ← Commands, Queries, Handlers, Validators, DTOs
├── Infrastructure/ImperadorBarberShop.Infrastructure/ ← EF Core, Repositories, JwtService, BCrypt, SMTP
└── Api/ImperadorBarberShop.Api/                 ← Controllers (thin), Program.cs, Middleware

tests/
├── ImperadorBarberShop.UnitTests/               ← xUnit + NSubstitute + FluentAssertions
└── ImperadorBarberShop.IntegrationTests/        ← WebApplicationFactory + Testcontainers.PostgreSql
```

## Key Design Decisions

- **Clean Architecture**: Domain has zero external dependencies. Application depends only on Domain. Infrastructure and Api depend on Application.
- **CQRS with MediatR**: Every use case is a Command (write) or Query (read). Controllers are thin dispatchers.
- **Co-located handlers**: Each command/query file contains the record + validator + handler in one `.cs` file.
- **JWT claim mapping cleared**: `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` preserves original claim names (`sub`, `role`, `barberId`).
- **Refresh token security**: Raw token returned to client; only BCrypt hash stored in DB. Token is rotated (revoked + new one issued) on every refresh.
- **IDOR protection**: Every resource mutation validates that the JWT's `sub`/`barberId` claim matches the resource owner.

## Running the API

```bash
# Prerequisites: docker-compose up -d (starts PostgreSQL)
# Ensure appsettings.Development.json has connection string + JWT settings (see root CLAUDE.md)

cd backend
dotnet run --project src/Api/ImperadorBarberShop.Api

# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

## Running Tests

```bash
cd backend

# Unit tests (fast, no DB)
dotnet test tests/ImperadorBarberShop.UnitTests

# Integration tests (spins up PostgreSQL container via Testcontainers — requires Docker)
dotnet test tests/ImperadorBarberShop.IntegrationTests

# All tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## EF Core Migrations

```bash
# Create a migration (run from backend/)
dotnet ef migrations add <MigrationName> \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api

# Apply migrations
dotnet ef database update \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```

Migrations are also applied automatically on startup in Development (`db.Database.MigrateAsync()`).

## Environment Variables

All sensitive config is in `appsettings.Development.json` (gitignored). See root `CLAUDE.md` for the full structure.

Required keys:
- `ConnectionStrings:DefaultConnection` — PostgreSQL connection string
- `Jwt:Secret` — min 256-bit random string (e.g. `openssl rand -base64 32`)
- `Jwt:Issuer`
- `Jwt:Audience`
- `Email:SmtpHost`, `Email:SmtpPort`, `Email:Username`, `Email:Password`, `Email:FromAddress`
- `FrontendUrl` — e.g. `http://localhost:3000` (used for CORS)

## Authorization Policies

| Policy | Required JWT claim |
|--------|--------------------|
| `RequireClientRole` | `role == "Client"` |
| `RequireBarberRole` | `role == "Barber"` |
