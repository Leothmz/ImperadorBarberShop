# Agendamento Público (sem cadastro de cliente) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove client accounts entirely; clients book by submitting name+WhatsApp phone+barber+service+slot with no login. Appointments auto-confirm. Clients manage (cancel/review) via a unique token link.

**Architecture:** Domain `Appointment` drops `ClientId`/`Client` nav, gains `ClientName`, `ClientPhone`, `AccessToken`. `AppointmentStatus` collapses to `{Accepted, Cancelled, Completed}` — appointments are born `Accepted` (no manual barber approval step). Anonymous booking endpoint replaces JWT-derived client identity; a token-keyed "manage" endpoint group replaces client-authenticated cancel/review.

**Tech Stack:** .NET 9 / EF Core 9 / MediatR / FluentValidation / AutoMapper (backend), Next.js 15 / TanStack Query / Axios (frontend). No new dependencies.

## Global Constraints

- Backend: every command/query handler change must keep the co-located record+validator+handler-in-one-file pattern (per `backend/CLAUDE.md`).
- Backend: run `dotnet test tests/ImperadorBarberShop.UnitTests` after every task that touches Application/Domain code.
- Frontend: all UI text in Brazilian Portuguese; phone format `+55DDDXXXXXXXXX`.
- Do not implement WhatsApp sending, Admin/Barber role split, or the cash dashboard — those are separate specs (2/3/5). This plan only adds the `AccessToken`/manage-link plumbing they will consume later.
- IDOR pattern from `backend/CLAUDE.md` still applies: every barber-authenticated mutation must check `appointment.BarberId == barberId` from the JWT `barberId` claim.

---

## Task 1: Domain — `AppointmentStatus` enum and `Appointment` entity

**Files:**
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Enums/AppointmentStatus.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Appointment.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/AppointmentTests.cs`

**Interfaces:**
- Produces: `Appointment.Create(string clientName, string clientPhone, Guid barberId, DateTime scheduledAt, int totalDurationMinutes, string? notes, IEnumerable<Guid> serviceIds) -> Appointment` (status born `Accepted`, `AccessToken` auto-generated). `appointment.Cancel()`, `appointment.Complete()`. Public props: `ClientName`, `ClientPhone`, `AccessToken`, `BarberId`, `ScheduledAt`, `TotalDurationMinutes`, `Status`, `Notes`, `CreatedAt`, `UpdatedAt`, `Barber`, `AppointmentServices`. `Accept()`/`Reject()`/`ClientId`/`Client` no longer exist.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class AppointmentTests
{
    [Fact]
    public void Create_ValidInput_IsBornAccepted_WithUniqueAccessToken()
    {
        var a1 = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        var a2 = Appointment.Create("Maria", "+5511999990001", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        a1.Status.Should().Be(AppointmentStatus.Accepted);
        a1.ClientName.Should().Be("João");
        a1.ClientPhone.Should().Be("+5511999990000");
        a1.AccessToken.Should().NotBeNullOrEmpty();
        a1.AccessToken.Should().NotBe(a2.AccessToken);
    }

    [Fact]
    public void Cancel_WhenAccepted_SetsCancelled()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        appointment.Cancel();

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Cancel();

        var act = () => appointment.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_WhenAccepted_SetsCompleted()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        appointment.Complete();

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter AppointmentTests`
Expected: FAIL (compile error — `Appointment.Create` still takes `Guid clientId` as first parameter, no `AccessToken` property)

- [ ] **Step 3: Replace the enum**

```csharp
namespace ImperadorBarberShop.Domain.Enums;

public enum AppointmentStatus
{
    Accepted = 0,
    Cancelled = 1,
    Completed = 2
}
```

- [ ] **Step 4: Replace the entity**

```csharp
using System.Security.Cryptography;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Domain.Entities;

public class Appointment
{
    private readonly List<AppointmentService> _appointmentServices = new();

    public Guid Id { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string ClientPhone { get; private set; } = string.Empty;
    public string AccessToken { get; private set; } = string.Empty;
    public Guid BarberId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public int TotalDurationMinutes { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Barber Barber { get; private set; } = null!;
    public IReadOnlyCollection<AppointmentService> AppointmentServices => _appointmentServices.AsReadOnly();

    // EF Core constructor
    private Appointment() { }

    public static Appointment Create(
        string clientName,
        string clientPhone,
        Guid barberId,
        DateTime scheduledAt,
        int totalDurationMinutes,
        string? notes,
        IEnumerable<Guid> serviceIds)
    {
        var now = DateTime.UtcNow;
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClientName = clientName,
            ClientPhone = clientPhone,
            AccessToken = GenerateAccessToken(),
            BarberId = barberId,
            ScheduledAt = scheduledAt,
            TotalDurationMinutes = totalDurationMinutes,
            Status = AppointmentStatus.Accepted,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var serviceId in serviceIds)
            appointment._appointmentServices.Add(AppointmentService.Create(appointment.Id, serviceId));

        return appointment;
    }

    public void Cancel()
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot cancel appointment in status {Status}.");
        Status = AppointmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot complete appointment in status {Status}.");
        Status = AppointmentStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string GenerateAccessToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter AppointmentTests`
Expected: PASS (4 tests) — the rest of the solution will not compile yet; that's expected until later tasks land. Confirm via `dotnet build tests/ImperadorBarberShop.UnitTests` that the *only* errors remaining are in files this plan still has to touch (other `Appointment.Create(...)` call sites, `Appointment.Accept()`/`.Reject()` call sites).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Enums/AppointmentStatus.cs backend/src/Domain/ImperadorBarberShop.Domain/Entities/Appointment.cs backend/tests/ImperadorBarberShop.UnitTests/Appointments/AppointmentTests.cs
git commit -m "feat(domain): anonymous Appointment with ClientName/Phone/AccessToken, auto-accept"
```

---

## Task 2: Domain — `Review` entity (drop `ClientId`)

**Files:**
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Review.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Reviews/ReviewTests.cs`

**Interfaces:**
- Produces: `Review.Create(Guid appointmentId, Guid barberId, int rating, string? comment) -> Review`. `ClientId` no longer exists.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.UnitTests.Reviews;

public class ReviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_RatingOutOfRange_Throws(int rating)
    {
        var act = () => Review.Create(Guid.NewGuid(), Guid.NewGuid(), rating, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_ValidRating_SetsFields()
    {
        var appointmentId = Guid.NewGuid();
        var barberId = Guid.NewGuid();

        var review = Review.Create(appointmentId, barberId, 5, "Top!");

        review.AppointmentId.Should().Be(appointmentId);
        review.BarberId.Should().Be(barberId);
        review.Rating.Should().Be(5);
        review.Comment.Should().Be("Top!");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter ReviewTests`
Expected: FAIL (compile error — `Review.Create` still requires a `clientId` argument)

- [ ] **Step 3: Replace the entity**

```csharp
namespace ImperadorBarberShop.Domain.Entities;

public class Review
{
    public Guid Id { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid BarberId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Appointment Appointment { get; private set; } = null!;

    // EF Core constructor
    private Review() { }

    public static Review Create(Guid appointmentId, Guid barberId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        return new Review
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            BarberId = barberId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter ReviewTests`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Entities/Review.cs backend/tests/ImperadorBarberShop.UnitTests/Reviews/ReviewTests.cs
git commit -m "feat(domain): drop Review.ClientId — clients no longer have accounts"
```

---

## Task 3: Domain — `IAppointmentRepository` interface

**Files:**
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppointmentRepository.cs`

**Interfaces:**
- Produces: `GetByAccessTokenAsync(string accessToken, CancellationToken ct = default) -> Task<Appointment?>`, `CountCreatedByPhoneSinceAsync(string clientPhone, DateTime since, CancellationToken ct = default) -> Task<int>`. `GetByClientIdAsync` removed (no client identity left to query by).
- Consumes: nothing new.

This is a pure interface edit (no behavior to unit-test directly — covered by the handler tests in Tasks 6-8 that consume it through the repository implementation built in Task 4).

- [ ] **Step 1: Replace the interface**

```csharp
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Appointment?> GetByAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetActiveByBarberIdAndDateAsync(Guid barberId, DateOnly date, CancellationToken cancellationToken = default);
    Task<int> CountCreatedByPhoneSinceAsync(string clientPhone, DateTime since, CancellationToken cancellationToken = default);
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Build to confirm the interface compiles standalone**

Run: `cd backend && dotnet build src/Domain/ImperadorBarberShop.Domain`
Expected: succeeds (Domain project has zero dependents inside itself, so this always compiles in isolation)

- [ ] **Step 3: Commit**

```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppointmentRepository.cs
git commit -m "feat(domain): IAppointmentRepository gains token/phone lookups, drops client lookup"
```

---

## Task 4: Infrastructure — repository, EF config, DTOs, mapping

**Files:**
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppointmentRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/DTOs/AppointmentDto.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/DTOs/AppointmentManageDto.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Mappings/MappingProfile.cs`

**Interfaces:**
- Consumes: `IAppointmentRepository` (Task 3), `Appointment` (Task 1).
- Produces: `AppointmentDto` (barber-facing list — now has `ClientPhone`, no `ClientId`). `AppointmentManageDto` (public token page — `Id, ClientName, BarberName, ScheduledAt, TotalDurationMinutes, Status, Services`).

No new unit test here — these are thin EF/AutoMapper wrappers already exercised indirectly by the handler tests in Tasks 6-8, which is the pragmatic test boundary used elsewhere in this codebase (`AppointmentRepository` has no dedicated unit tests today either).

- [ ] **Step 1: Replace `AppointmentRepository.cs`**

```csharp
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _context;

    public AppointmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Appointment?> GetByAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(a => a.AccessToken == accessToken, cancellationToken);

    public async Task<List<Appointment>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .Where(a => a.BarberId == barberId)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<List<Appointment>> GetActiveByBarberIdAndDateAsync(
        Guid barberId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.Appointments
            .Where(a => a.BarberId == barberId
                && a.ScheduledAt >= dayStart
                && a.ScheduledAt <= dayEnd
                && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountCreatedByPhoneSinceAsync(string clientPhone, DateTime since, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .CountAsync(a => a.ClientPhone == clientPhone && a.CreatedAt >= since, cancellationToken);

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
        => await _context.Appointments.AddAsync(appointment, cancellationToken);

    public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _context.Appointments.Update(appointment);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Replace `AppointmentConfiguration.cs`**

```csharp
using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ClientName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ClientPhone).IsRequired().HasMaxLength(20);
        builder.Property(a => a.AccessToken).IsRequired().HasMaxLength(64);
        builder.Property(a => a.ScheduledAt).IsRequired();
        builder.Property(a => a.TotalDurationMinutes).IsRequired();
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(500);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasIndex(a => new { a.BarberId, a.ScheduledAt }).IsUnique();
        builder.HasIndex(a => a.AccessToken).IsUnique();

        builder.HasMany(a => a.AppointmentServices)
            .WithOne()
            .HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Private backing field
        builder.Navigation(a => a.AppointmentServices).HasField("_appointmentServices");
    }
}
```

- [ ] **Step 3: Replace `AppointmentDto.cs`**

```csharp
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Application.DTOs;

public record AppointmentDto
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string ClientPhone { get; init; } = string.Empty;
    public Guid BarberId { get; init; }
    public string BarberName { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public int TotalDurationMinutes { get; init; }
    public AppointmentStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<ServiceDto> Services { get; init; } = [];
}
```

- [ ] **Step 4: Create `AppointmentManageDto.cs`**

```csharp
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Application.DTOs;

public record AppointmentManageDto
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string BarberName { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public int TotalDurationMinutes { get; init; }
    public AppointmentStatus Status { get; init; }
    public List<ServiceDto> Services { get; init; } = [];
}
```

- [ ] **Step 5: Update `MappingProfile.cs`**

```csharp
using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Service, ServiceDto>();

        CreateMap<Barber, BarberDto>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.User.Name))
            .ForMember(d => d.Email, o => o.MapFrom(s => s.User.Email))
            .ForMember(d => d.Availability, o => o.MapFrom(s => s.Availability));

        CreateMap<BarberAvailability, BarberAvailabilityDto>();

        CreateMap<Appointment, AppointmentDto>()
            .ForMember(d => d.BarberName, o => o.MapFrom(s => s.Barber.User.Name))
            .ForMember(d => d.Services, o => o.MapFrom(s => s.AppointmentServices.Select(a => a.Service)));

        CreateMap<Appointment, AppointmentManageDto>()
            .ForMember(d => d.BarberName, o => o.MapFrom(s => s.Barber.User.Name))
            .ForMember(d => d.Services, o => o.MapFrom(s => s.AppointmentServices.Select(a => a.Service)));

        CreateMap<Review, ReviewDto>();
    }
}
```

- [ ] **Step 6: Build the Infrastructure and Application projects**

Run: `cd backend && dotnet build src/Infrastructure/ImperadorBarberShop.Infrastructure && dotnet build src/Application/ImperadorBarberShop.Application`
Expected: still fails — `CreateAppointmentCommand`, `AcceptAppointmentCommand`, `RejectAppointmentCommand`, `CancelAppointmentCommand`, `CreateReviewCommand`, `GetClientAppointmentsQuery` all still reference the old `Appointment.Create`/`Review.Create` signatures and `ClientId`. That's expected — Tasks 6-9 fix them next.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppointmentRepository.cs backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs backend/src/Application/ImperadorBarberShop.Application/DTOs/AppointmentDto.cs backend/src/Application/ImperadorBarberShop.Application/DTOs/AppointmentManageDto.cs backend/src/Application/ImperadorBarberShop.Application/Mappings/MappingProfile.cs
git commit -m "feat(infra): repository/EF-config/DTO support for token-based anonymous appointments"
```

---

## Task 5: Infrastructure — EF migration (schema + data backfill)

**Files:**
- Create (generated then hand-edited): `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/<timestamp>_RemoveClientAccountModel.cs`

**Interfaces:**
- Consumes: the model shape from Tasks 1, 2, 4 (EF will diff against `AppDbContextModelSnapshot.cs`).
- Produces: a migration that can run against existing dev data without breaking it.

No automated test — verified by running `dotnet ef database update` against the local Postgres (via `docker-compose up -d`) and confirming it applies cleanly.

- [ ] **Step 1: Generate the migration scaffold**

Run (from `backend/`):
```bash
dotnet ef migrations add RemoveClientAccountModel \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```
Expected: a new file `Migrations/<timestamp>_RemoveClientAccountModel.cs` is generated. It will contain `AddColumn` calls for `ClientName`/`ClientPhone`/`AccessToken` as **NOT NULL with no default** and `DropColumn`/`DropForeignKey` for `ClientId` on `Appointments`, plus `DropColumn` for `ClientId` on `Reviews`. Because the new columns are NOT NULL, this generated version will fail against any existing rows — Step 2 rewrites it.

- [ ] **Step 2: Hand-edit the generated migration's `Up`/`Down` methods**

Replace the body of the generated `Up(MigrationBuilder migrationBuilder)` method with this exact sequence (order matters — add nullable, backfill, then tighten):

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Add the new columns as nullable so existing rows don't fail the NOT NULL constraint yet.
    migrationBuilder.AddColumn<string>(
        name: "ClientName",
        table: "Appointments",
        type: "character varying(100)",
        maxLength: 100,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "ClientPhone",
        table: "Appointments",
        type: "character varying(20)",
        maxLength: 20,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "AccessToken",
        table: "Appointments",
        type: "character varying(64)",
        maxLength: 64,
        nullable: true);

    // 2. Backfill from the Users table (still intact at this point in the migration).
    migrationBuilder.Sql(@"
        UPDATE ""Appointments"" a
        SET ""ClientName"" = COALESCE(u.""Name"", 'Cliente'),
            ""ClientPhone"" = '+550000000000'
        FROM ""Users"" u
        WHERE u.""Id"" = a.""ClientId"";
    ");

    // 3. Generate a unique opaque token per existing row (good enough for backfilled rows;
    // all rows created after this migration get a cryptographically random token from
    // Appointment.Create in application code).
    migrationBuilder.Sql(@"
        UPDATE ""Appointments""
        SET ""AccessToken"" = md5(""Id""::text || random()::text || clock_timestamp()::text)
                            || md5(random()::text || clock_timestamp()::text);
    ");

    // 4. Remap AppointmentStatus int values to the collapsed enum:
    //    old Pending(0) -> new Accepted(0)
    //    old Accepted(1) -> new Accepted(0)
    //    old Rejected(2) -> new Cancelled(1)
    //    old Cancelled(3) -> new Cancelled(1)
    //    old Completed(4) -> new Completed(2)
    migrationBuilder.Sql(@"
        UPDATE ""Appointments""
        SET ""Status"" = CASE ""Status""
            WHEN 0 THEN 0
            WHEN 1 THEN 0
            WHEN 2 THEN 1
            WHEN 3 THEN 1
            WHEN 4 THEN 2
        END;
    ");

    // 5. Now that every row has values, tighten the new columns to NOT NULL.
    migrationBuilder.AlterColumn<string>(
        name: "ClientName",
        table: "Appointments",
        type: "character varying(100)",
        maxLength: 100,
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(100)",
        oldNullable: true);

    migrationBuilder.AlterColumn<string>(
        name: "ClientPhone",
        table: "Appointments",
        type: "character varying(20)",
        maxLength: 20,
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(20)",
        oldNullable: true);

    migrationBuilder.AlterColumn<string>(
        name: "AccessToken",
        table: "Appointments",
        type: "character varying(64)",
        maxLength: 64,
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(64)",
        oldNullable: true);

    migrationBuilder.CreateIndex(
        name: "IX_Appointments_AccessToken",
        table: "Appointments",
        column: "AccessToken",
        unique: true);

    // 6. Drop the old client FK/column from Appointments and Reviews.
    migrationBuilder.DropForeignKey(
        name: "FK_Appointments_Users_ClientId",
        table: "Appointments");

    migrationBuilder.DropColumn(
        name: "ClientId",
        table: "Appointments");

    migrationBuilder.DropColumn(
        name: "ClientId",
        table: "Reviews");

    // 7. Client accounts no longer exist — remove them (UserRole.Client = 0).
    migrationBuilder.Sql(@"DELETE FROM ""Users"" WHERE ""Role"" = 0;");
}
```

Replace the body of `Down(MigrationBuilder migrationBuilder)` with:

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    throw new NotSupportedException(
        "This migration deletes client account data and collapses AppointmentStatus values. " +
        "Rolling back would require restoring from a pre-migration backup, not a generated Down().");
}
```

> Note: the exact column-type strings (`character varying(100)` etc.) and FK/index names (`FK_Appointments_Users_ClientId`) must match what EF actually generated in Step 1 for this database provider/model — copy them from the generated file rather than retyping, since EF's naming convention can vary slightly by EF Core version. Adjust the snippets above to match if they differ.

- [ ] **Step 3: Apply the migration**

Run (from `backend/`, with `docker-compose up -d` already running):
```bash
dotnet ef database update \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```
Expected: `Done.` with no errors. If the dev database has zero rows (fresh container), the backfill SQL is a no-op and this still succeeds.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/
git commit -m "feat(db): migrate Appointments/Reviews off client accounts, collapse AppointmentStatus"
```

---

## Task 6: Application — anonymous `CreateAppointmentCommand`

**Files:**
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CreateAppointmentCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Interfaces/IEmailService.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/SmtpEmailService.cs`
- Modify: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/CreateAppointmentCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `Appointment.Create` (Task 1), `IAppointmentRepository.CountCreatedByPhoneSinceAsync` (Task 3/4).
- Produces: `CreateAppointmentCommand(string ClientName, string ClientPhone, Guid BarberId, DateTime ScheduledAt, List<Guid> ServiceIds, string? Notes) : IRequest<CreateAppointmentResult>`. `CreateAppointmentResult(Guid Id, string AccessToken)`. Used by Task 10 (controller).

- [ ] **Step 1: Write the failing tests (rewrite the whole file)**

```csharp
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CreateAppointmentCommandHandlerTests
{
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IServiceRepository _serviceRepository = Substitute.For<IServiceRepository>();
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateAppointmentCommandHandler _handler;

    public CreateAppointmentCommandHandlerTests()
    {
        _handler = new CreateAppointmentCommandHandler(
            _barberRepository, _serviceRepository, _appointmentRepository, _emailService, _unitOfWork);
    }

    private void SetupHappyPath(Guid barberId, Service service)
    {
        var barberUser = User.CreateBarber("Carlos", "carlos@email.com", "hash");
        var barber = Barber.Create(barberUser.Id);
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _appointmentRepository.CountCreatedByPhoneSinceAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsIdAndAccessToken()
    {
        var barberId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        SetupHappyPath(barberId, service);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { serviceId }, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.AccessToken.Should().NotBeNullOrEmpty();
        await _appointmentRepository.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BarberNotFound_ThrowsKeyNotFoundException()
    {
        _barberRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Barber?)null);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ServicesNotFound_ThrowsKeyNotFoundException()
    {
        var barberId = Guid.NewGuid();
        var barber = Barber.Create(Guid.NewGuid());
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service>());

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_TimeSlotOccupied_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var barber = Barber.Create(Guid.NewGuid());
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        var existingAppt = Appointment.Create("Maria", "+5511999990001", barberId, scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });
        _appointmentRepository.CountCreatedByPhoneSinceAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, scheduledAt, new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not available*");
    }

    [Fact]
    public async Task Handle_TooManyRecentRequestsFromPhone_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        SetupHappyPath(barberId, service);
        _appointmentRepository.CountCreatedByPhoneSinceAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Too many*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CreateAppointmentCommandHandlerTests`
Expected: FAIL (compile error — `CreateAppointmentCommandHandler` constructor still takes `IUserRepository` and `ClientId`; no `CreateAppointmentResult`)

- [ ] **Step 3: Replace `IEmailService.cs`**

```csharp
namespace ImperadorBarberShop.Application.Interfaces;

public interface IEmailService
{
    Task SendAppointmentCreatedAsync(string barberEmail, string barberName, string clientName, string clientPhone, DateTime scheduledAt, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Replace `SmtpEmailService.cs`**

```csharp
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ImperadorBarberShop.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public SmtpEmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAppointmentCreatedAsync(
        string barberEmail, string barberName, string clientName, string clientPhone, DateTime scheduledAt,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Novo agendamento de {clientName}";
        var body = $"Olá {barberName},\n\n" +
                   $"O cliente {clientName} (WhatsApp {clientPhone}) agendou um atendimento para {scheduledAt:dd/MM/yyyy HH:mm}.\n\n" +
                   "O agendamento já está confirmado automaticamente.\n\n" +
                   "O Imperador Barber Shop";

        await SendAsync(barberEmail, subject, body, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
```

- [ ] **Step 5: Replace `CreateAppointmentCommand.cs`**

```csharp
using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CreateAppointmentCommand(
    string ClientName,
    string ClientPhone,
    Guid BarberId,
    DateTime ScheduledAt,
    List<Guid> ServiceIds,
    string? Notes) : IRequest<CreateAppointmentResult>;

public record CreateAppointmentResult(Guid Id, string AccessToken);

public class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentCommandValidator()
    {
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ClientPhone).NotEmpty()
            .Matches(@"^\+55\d{10,11}$")
            .WithMessage("ClientPhone must be in the format +55DDDXXXXXXXXX.");
        RuleFor(x => x.BarberId).NotEmpty();
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTime.UtcNow)
            .WithMessage("ScheduledAt must be in the future.");
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("At least one service is required.");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
    }
}

public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, CreateAppointmentResult>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentCommandHandler(
        IBarberRepository barberRepository,
        IServiceRepository serviceRepository,
        IAppointmentRepository appointmentRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _serviceRepository = serviceRepository;
        _appointmentRepository = appointmentRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateAppointmentResult> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken);
        if (barber is null)
            throw new KeyNotFoundException($"Barber '{request.BarberId}' not found.");

        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count != request.ServiceIds.Count)
            throw new KeyNotFoundException("One or more services were not found.");

        // Anti-spam: cap appointment creation per phone number, independent of the
        // per-IP rate limit applied at the HTTP layer (Task 11).
        var recentCount = await _appointmentRepository.CountCreatedByPhoneSinceAsync(
            request.ClientPhone, DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentCount >= 3)
            throw new InvalidOperationException("Too many appointment requests from this phone number. Try again later.");

        // Check slot availability — ensure no overlap with existing appointments
        var date = DateOnly.FromDateTime(request.ScheduledAt);
        var activeAppointments = await _appointmentRepository.GetActiveByBarberIdAndDateAsync(
            request.BarberId, date, cancellationToken);

        var totalDuration = services.Sum(s => s.DurationMinutes);
        var requestEnd = request.ScheduledAt.AddMinutes(totalDuration);

        foreach (var existing in activeAppointments)
        {
            var existingEnd = existing.ScheduledAt.AddMinutes(existing.TotalDurationMinutes);
            if (request.ScheduledAt < existingEnd && requestEnd > existing.ScheduledAt)
                throw new InvalidOperationException("The requested time slot is not available.");
        }

        var appointment = Appointment.Create(
            request.ClientName,
            request.ClientPhone,
            request.BarberId,
            request.ScheduledAt,
            totalDuration,
            request.Notes,
            request.ServiceIds);

        await _appointmentRepository.AddAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification to barber (best-effort — email failure must not roll back the appointment)
        if (barber.User is not null)
        {
            try
            {
                await _emailService.SendAppointmentCreatedAsync(
                    barber.User.Email,
                    barber.User.Name,
                    request.ClientName,
                    request.ClientPhone,
                    request.ScheduledAt,
                    cancellationToken);
            }
            catch
            {
                // Notification failure is non-critical; appointment is already persisted
            }
        }

        return new CreateAppointmentResult(appointment.Id, appointment.AccessToken);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CreateAppointmentCommandHandlerTests`
Expected: PASS (5 tests)

- [ ] **Step 7: Commit**

```bash
git add backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CreateAppointmentCommand.cs backend/src/Application/ImperadorBarberShop.Application/Interfaces/IEmailService.cs backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/SmtpEmailService.cs backend/tests/ImperadorBarberShop.UnitTests/Appointments/CreateAppointmentCommandHandlerTests.cs
git commit -m "feat(application): anonymous appointment creation with phone-based anti-spam check"
```

---

## Task 7: Application — drop Accept/Reject, add `CancelAppointmentByBarberCommand`

**Files:**
- Delete: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/AcceptAppointmentCommand.cs`
- Delete: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/RejectAppointmentCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentByBarberCommand.cs`
- Delete: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/AcceptRejectCompleteCommandHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/CancelAppointmentByBarberCommandHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/CompleteAppointmentCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `appointment.Cancel()` (Task 1), `IAppointmentRepository`, `ForbiddenException` (`ImperadorBarberShop.Domain.Exceptions`).
- Produces: `CancelAppointmentByBarberCommand(Guid AppointmentId, Guid BarberId) : IRequest`. Used by Task 10 (controller, replaces the old `/reject` route as `/cancel-by-barber`).
- `CompleteAppointmentCommand`/`CompleteAppointmentCommandHandler` are unchanged from the existing code — this task only moves their tests into a dedicated file (the old combined file is deleted) and removes the now-invalid `appointment.Accept()` setup calls.

- [ ] **Step 1: Delete the old files**

```bash
git rm backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/AcceptAppointmentCommand.cs
git rm backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/RejectAppointmentCommand.cs
git rm backend/tests/ImperadorBarberShop.UnitTests/Appointments/AcceptRejectCompleteCommandHandlerTests.cs
```

- [ ] **Step 2: Write the failing test for `CancelAppointmentByBarberCommand`**

```csharp
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CancelAppointmentByBarberCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelAppointmentByBarberCommandHandler _handler;

    public CancelAppointmentByBarberCommandHandlerTests()
    {
        _handler = new CancelAppointmentByBarberCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCancel_CancelsAppointment()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CancelAppointmentByBarberCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        await _appointmentRepository.Received(1).UpdateAsync(appointment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CancelAppointmentByBarberCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbiddenException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentByBarberCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Cancel();
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentByBarberCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CancelAppointmentByBarberCommandHandlerTests`
Expected: FAIL (compile error — `CancelAppointmentByBarberCommand` doesn't exist yet)

- [ ] **Step 4: Create `CancelAppointmentByBarberCommand.cs`**

```csharp
using FluentValidation;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CancelAppointmentByBarberCommand(Guid AppointmentId, Guid BarberId) : IRequest;

public class CancelAppointmentByBarberCommandValidator : AbstractValidator<CancelAppointmentByBarberCommand>
{
    public CancelAppointmentByBarberCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
    }
}

public class CancelAppointmentByBarberCommandHandler : IRequestHandler<CancelAppointmentByBarberCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentByBarberCommandHandler(
        IAppointmentRepository appointmentRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelAppointmentByBarberCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (appointment.BarberId != request.BarberId)
            throw new ForbiddenException("You are not authorized to cancel this appointment.");

        appointment.Cancel();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CancelAppointmentByBarberCommandHandlerTests`
Expected: PASS (4 tests)

- [ ] **Step 6: Create `CompleteAppointmentCommandHandlerTests.cs`** (carries over the still-valid Complete tests, with `Accept()` calls removed since appointments are now born `Accepted`)

```csharp
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CompleteAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CompleteAppointmentCommandHandler _handler;

    public CompleteAppointmentCommandHandlerTests()
    {
        _handler = new CompleteAppointmentCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidComplete_CompletesAppointment()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbiddenException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Complete();
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

> Note: if `Handle_WrongBarber_*` tests above don't compile as `ThrowAsync<ForbiddenException>` against the existing `AcceptAppointmentCommandHandler`/`RejectAppointmentCommandHandler`/`CompleteAppointmentCommandHandler` source (which already throws `ForbiddenException`, not `UnauthorizedAccessException`), that confirms the pre-existing test file had a latent mismatch — this rewrite fixes it as a side effect, no separate task needed.

- [ ] **Step 7: Run the full Appointments test folder**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "FullyQualifiedName~Appointments"`
Expected: PASS (all tests in `CreateAppointmentCommandHandlerTests`, `CancelAppointmentByBarberCommandHandlerTests`, `CompleteAppointmentCommandHandlerTests`, `AppointmentTests`)

- [ ] **Step 8: Commit**

```bash
git add backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentByBarberCommand.cs backend/tests/ImperadorBarberShop.UnitTests/Appointments/
git commit -m "feat(application): replace manual accept/reject with auto-confirm + barber-initiated cancel"
```

---

## Task 8: Application — token-based manage/cancel/review

**Files:**
- Delete: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentCommand.cs`
- Delete: `backend/src/Application/ImperadorBarberShop.Application/Queries/Appointments/GetClientAppointmentsQuery.cs`
- Delete: `backend/src/Application/ImperadorBarberShop.Application/Commands/Reviews/CreateReviewCommand.cs`
- Delete: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/CancelAppointmentCommandHandlerTests.cs`
- Delete: `backend/tests/ImperadorBarberShop.UnitTests/Reviews/CreateReviewCommandHandlerTests.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Appointments/GetAppointmentByTokenQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentByTokenCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Reviews/CreateReviewByTokenCommand.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/GetAppointmentByTokenQueryHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/CancelAppointmentByTokenCommandHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Reviews/CreateReviewByTokenCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppointmentRepository.GetByAccessTokenAsync` (Task 3/4), `AppointmentManageDto` (Task 4), `Review.Create(Guid, Guid, int, string?)` (Task 2), `IReviewRepository`, `IBarberRepository`.
- Produces: `GetAppointmentByTokenQuery(string AccessToken) : IRequest<AppointmentManageDto>`. `CancelAppointmentByTokenCommand(string AccessToken) : IRequest`. `CreateReviewByTokenCommand(string AccessToken, int Rating, string? Comment) : IRequest<Guid>`. Used by Task 10 (controller).

- [ ] **Step 1: Delete the old files**

```bash
git rm backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentCommand.cs
git rm backend/src/Application/ImperadorBarberShop.Application/Queries/Appointments/GetClientAppointmentsQuery.cs
git rm backend/src/Application/ImperadorBarberShop.Application/Commands/Reviews/CreateReviewCommand.cs
git rm backend/tests/ImperadorBarberShop.UnitTests/Appointments/CancelAppointmentCommandHandlerTests.cs
git rm backend/tests/ImperadorBarberShop.UnitTests/Reviews/CreateReviewCommandHandlerTests.cs
```

- [ ] **Step 2: Write the failing test for `GetAppointmentByTokenQuery`**

```csharp
using AutoMapper;
using FluentAssertions;
using ImperadorBarberShop.Application.Mappings;
using ImperadorBarberShop.Application.Queries.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class GetAppointmentByTokenQueryHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IMapper _mapper;
    private readonly GetAppointmentByTokenQueryHandler _handler;

    public GetAppointmentByTokenQueryHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        _handler = new GetAppointmentByTokenQueryHandler(_appointmentRepository, _mapper);
    }

    [Fact]
    public async Task Handle_TokenFound_ReturnsDto()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>())
            .Returns(appointment);

        var result = await _handler.Handle(new GetAppointmentByTokenQuery(appointment.AccessToken), CancellationToken.None);

        result.Id.Should().Be(appointment.Id);
        result.ClientName.Should().Be("João");
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(new GetAppointmentByTokenQuery("bogus"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter GetAppointmentByTokenQueryHandlerTests`
Expected: FAIL (compile error — `GetAppointmentByTokenQuery` doesn't exist yet)

- [ ] **Step 4: Create `GetAppointmentByTokenQuery.cs`**

```csharp
using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Appointments;

public record GetAppointmentByTokenQuery(string AccessToken) : IRequest<AppointmentManageDto>;

public class GetAppointmentByTokenQueryHandler : IRequestHandler<GetAppointmentByTokenQuery, AppointmentManageDto>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public GetAppointmentByTokenQueryHandler(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<AppointmentManageDto> Handle(GetAppointmentByTokenQuery request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        return _mapper.Map<AppointmentManageDto>(appointment);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter GetAppointmentByTokenQueryHandlerTests`
Expected: PASS (2 tests)

- [ ] **Step 6: Write the failing test for `CancelAppointmentByTokenCommand`**

```csharp
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CancelAppointmentByTokenCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelAppointmentByTokenCommandHandler _handler;

    public CancelAppointmentByTokenCommandHandlerTests()
    {
        _handler = new CancelAppointmentByTokenCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCancel_CancelsAppointment()
    {
        var scheduledAt = DateTime.UtcNow.AddHours(3); // > 2h in the future
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CancelAppointmentByTokenCommand("bogus"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_LessThan2HoursBeforeSchedule_ThrowsInvalidOperationException()
    {
        var scheduledAt = DateTime.UtcNow.AddMinutes(90); // less than 2 hours
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2 hours*");
    }
}
```

- [ ] **Step 7: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CancelAppointmentByTokenCommandHandlerTests`
Expected: FAIL (compile error — type doesn't exist yet)

- [ ] **Step 8: Create `CancelAppointmentByTokenCommand.cs`**

```csharp
using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CancelAppointmentByTokenCommand(string AccessToken) : IRequest;

public class CancelAppointmentByTokenCommandValidator : AbstractValidator<CancelAppointmentByTokenCommand>
{
    public CancelAppointmentByTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
    }
}

public class CancelAppointmentByTokenCommandHandler : IRequestHandler<CancelAppointmentByTokenCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentByTokenCommandHandler(
        IAppointmentRepository appointmentRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelAppointmentByTokenCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        // Rule: ScheduledAt must be MORE THAN 2 hours away; "exactly 2h" is not enough — use <=
        if (appointment.ScheduledAt - DateTime.UtcNow <= TimeSpan.FromHours(2))
            throw new InvalidOperationException("Cannot cancel an appointment within 2 hours of the scheduled time.");

        appointment.Cancel();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 9: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CancelAppointmentByTokenCommandHandlerTests`
Expected: PASS (3 tests)

- [ ] **Step 10: Write the failing test for `CreateReviewByTokenCommand`**

```csharp
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Reviews;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Reviews;

public class CreateReviewByTokenCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IReviewRepository _reviewRepository = Substitute.For<IReviewRepository>();
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateReviewByTokenCommandHandler _handler;

    public CreateReviewByTokenCommandHandlerTests()
    {
        _handler = new CreateReviewByTokenCommandHandler(
            _appointmentRepository, _reviewRepository, _barberRepository, _unitOfWork);
    }

    private static Appointment CreateCompletedAppointment(Guid barberId)
    {
        var appt = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddHours(-2), 30, null, new[] { Guid.NewGuid() });
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_ValidReview_ReturnsReviewId()
    {
        var barberId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(barberId);
        var barber = Barber.Create(barberId);

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _reviewRepository.GetByAppointmentIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns((Review?)null);
        _reviewRepository.GetAverageRatingByBarberIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(4.5m);
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new CreateReviewByTokenCommand(appointment.AccessToken, 5, "Ótimo atendimento!");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _reviewRepository.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CreateReviewByTokenCommand("bogus", 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_AppointmentNotCompleted_ThrowsInvalidOperationException()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        // Status is Accepted, not Completed

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CreateReviewByTokenCommand(appointment.AccessToken, 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*completed*");
    }

    [Fact]
    public async Task Handle_DuplicateReview_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(barberId);
        var existingReview = Review.Create(appointment.Id, barberId, 4, null);

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _reviewRepository.GetByAppointmentIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(existingReview);

        var act = () => _handler.Handle(new CreateReviewByTokenCommand(appointment.AccessToken, 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been reviewed*");
    }
}
```

- [ ] **Step 11: Run test to verify it fails**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CreateReviewByTokenCommandHandlerTests`
Expected: FAIL (compile error — type doesn't exist yet)

- [ ] **Step 12: Create `CreateReviewByTokenCommand.cs`**

```csharp
using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Reviews;

public record CreateReviewByTokenCommand(
    string AccessToken,
    int Rating,
    string? Comment) : IRequest<Guid>;

public class CreateReviewByTokenCommandValidator : AbstractValidator<CreateReviewByTokenCommand>
{
    public CreateReviewByTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000).When(x => x.Comment is not null);
    }
}

public class CreateReviewByTokenCommandHandler : IRequestHandler<CreateReviewByTokenCommand, Guid>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateReviewByTokenCommandHandler(
        IAppointmentRepository appointmentRepository,
        IReviewRepository reviewRepository,
        IBarberRepository barberRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _reviewRepository = reviewRepository;
        _barberRepository = barberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateReviewByTokenCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        if (appointment.Status != AppointmentStatus.Completed)
            throw new InvalidOperationException("Can only review completed appointments.");

        var existing = await _reviewRepository.GetByAppointmentIdAsync(appointment.Id, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("This appointment has already been reviewed.");

        var review = Review.Create(appointment.Id, appointment.BarberId, request.Rating, request.Comment);

        await _reviewRepository.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedAverage = await _reviewRepository.GetAverageRatingByBarberIdAsync(appointment.BarberId, cancellationToken);
        var barber = await _barberRepository.GetByIdAsync(appointment.BarberId, cancellationToken);
        if (barber is not null)
        {
            barber.UpdateAverageRating(updatedAverage);
            await _barberRepository.UpdateAsync(barber, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return review.Id;
    }
}
```

- [ ] **Step 13: Run test to verify it passes**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter CreateReviewByTokenCommandHandlerTests`
Expected: PASS (4 tests)

- [ ] **Step 14: Commit**

```bash
git add backend/src/Application/ImperadorBarberShop.Application/Queries/Appointments/GetAppointmentByTokenQuery.cs backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentByTokenCommand.cs backend/src/Application/ImperadorBarberShop.Application/Commands/Reviews/CreateReviewByTokenCommand.cs backend/tests/ImperadorBarberShop.UnitTests/Appointments/GetAppointmentByTokenQueryHandlerTests.cs backend/tests/ImperadorBarberShop.UnitTests/Appointments/CancelAppointmentByTokenCommandHandlerTests.cs backend/tests/ImperadorBarberShop.UnitTests/Reviews/CreateReviewByTokenCommandHandlerTests.cs
git commit -m "feat(application): token-based appointment management and review submission"
```

---

## Task 9: Application — remove client registration

**Files:**
- Delete: `backend/src/Application/ImperadorBarberShop.Application/Commands/Auth/RegisterClientCommand.cs`
- Delete: `backend/tests/ImperadorBarberShop.UnitTests/Auth/RegisterClientCommandHandlerTests.cs`

**Interfaces:**
- Removes `RegisterClientCommand`/`RegisterClientCommandHandler` entirely. `User.CreateClient` (Domain) is left in place for now (harmless dead code removal is out of scope for this task — Domain cleanup isn't required for the API/frontend behavior change) but is no longer called anywhere after this task.

- [ ] **Step 1: Delete the files**

```bash
git rm backend/src/Application/ImperadorBarberShop.Application/Commands/Auth/RegisterClientCommand.cs
git rm backend/tests/ImperadorBarberShop.UnitTests/Auth/RegisterClientCommandHandlerTests.cs
```

- [ ] **Step 2: Build the Application project to confirm nothing else references it**

Run: `cd backend && dotnet build src/Application/ImperadorBarberShop.Application`
Expected: succeeds (the only consumer was `AuthController.RegisterClient`, fixed in Task 11)

- [ ] **Step 3: Commit**

```bash
git add -A backend/src/Application/ImperadorBarberShop.Application/Commands/Auth/ backend/tests/ImperadorBarberShop.UnitTests/Auth/
git commit -m "feat(application): remove client self-registration"
```

---

## Task 10: Api — rewrite `AppointmentsController`

**Files:**
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AppointmentsController.cs`

**Interfaces:**
- Consumes: `CreateAppointmentCommand`/`CreateAppointmentResult` (Task 6), `CancelAppointmentByBarberCommand` (Task 7), `GetAppointmentByTokenQuery`/`CancelAppointmentByTokenCommand`/`CreateReviewByTokenCommand` (Task 8), `GetBarberAppointmentsQuery`/`CompleteAppointmentCommand` (unchanged).
- Produces: routes consumed by Task 14/15 (frontend).

This is a controller-only change (thin dispatcher, no business logic) — verified by the integration tests rewritten in Task 12, not a new unit test here.

- [ ] **Step 1: Replace the file**

```csharp
using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Commands.Reviews;
using ImperadorBarberShop.Application.Queries.Appointments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImperadorBarberShop.Api.Controllers;

public record CreateAppointmentRequest(
    string ClientName,
    string ClientPhone,
    Guid BarberId,
    DateTime ScheduledAt,
    List<Guid> ServiceIds,
    string? Notes);

public record CreateReviewByTokenRequest(int Rating, string? Comment);

[ApiController]
[Route("api/v1/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AppointmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a new appointment. Public — no account required. Auto-confirmed.</summary>
    [HttpPost]
    [EnableRateLimiting("appointment-creation")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAppointmentCommand(
            request.ClientName, request.ClientPhone, request.BarberId,
            request.ScheduledAt, request.ServiceIds, request.Notes);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetByToken), new { token = result.AccessToken }, result);
    }

    /// <summary>Get an appointment's public status by its management token.</summary>
    [HttpGet("manage/{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByToken(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAppointmentByTokenQuery(token), cancellationToken);
        return Ok(result);
    }

    /// <summary>Cancel an appointment via its management token (must be >2h before scheduled time).</summary>
    [HttpPost("manage/{token}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelByToken(string token, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CancelAppointmentByTokenCommand(token), cancellationToken);
        return NoContent();
    }

    /// <summary>Submit a review via the management token (only once the appointment is Completed).</summary>
    [HttpPost("manage/{token}/review")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReviewByToken(
        string token,
        [FromBody] CreateReviewByTokenRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(
            new CreateReviewByTokenCommand(token, request.Rating, request.Comment), cancellationToken);
        return Created($"/api/v1/reviews/{id}", new { id });
    }

    /// <summary>List all appointments for the authenticated barber.</summary>
    [HttpGet("barber")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBarberAppointments(CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        var result = await _mediator.Send(new GetBarberAppointmentsQuery(barberId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Cancel a confirmed appointment (barber-initiated, e.g. emergencies).</summary>
    [HttpPatch("{id:guid}/cancel-by-barber")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelByBarber(Guid id, CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new CancelAppointmentByBarberCommand(id, barberId), cancellationToken);
        return NoContent();
    }

    /// <summary>Mark an accepted appointment as completed (barber only).</summary>
    [HttpPatch("{id:guid}/complete")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new CompleteAppointmentCommand(id, barberId), cancellationToken);
        return NoContent();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/src/Api/ImperadorBarberShop.Api/Controllers/AppointmentsController.cs
git commit -m "feat(api): rewrite AppointmentsController for anonymous booking + token-based management"
```

---

## Task 11: Api — `BarbersController`, `AuthController`, `Program.cs` rate limiter

**Files:**
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/BarbersController.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AuthController.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Program.cs`

**Interfaces:**
- Produces: a named rate-limiter policy `"appointment-creation"` consumed by `[EnableRateLimiting("appointment-creation")]` on `AppointmentsController.Create` (Task 10).

- [ ] **Step 1: In `BarbersController.cs`, remove the `[Authorize]` attribute from `GetSlots`**

```csharp
    /// <summary>Get available time slots for a barber on a given date for the selected services. Public.</summary>
    [HttpGet("{id:guid}/slots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots(
        Guid id,
        [FromQuery] DateOnly date,
        [FromQuery] List<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAvailableSlotsQuery(id, date, serviceIds), cancellationToken);
        return Ok(result);
    }
```

(Delete the old `[Authorize(Policy = "RequireClientRole")]` line and the now-unused `[ProducesResponseType(StatusCodes.Status401Unauthorized)]`/`Status403Forbidden` attributes above it. The `using Microsoft.AspNetCore.Authorization;` import at the top of the file stays — `UpdateAvailability` still uses `[Authorize(Policy = "RequireBarberRole")]`.)

- [ ] **Step 2: In `AuthController.cs`, remove the `RegisterClient` action**

Delete this whole method (and its `using ImperadorBarberShop.Application.Commands.Auth;` import stays, since `RegisterBarberCommand`/`LoginCommand`/`RefreshTokenCommand` still live in that namespace):

```csharp
    /// <summary>Register a new client account.</summary>
    [HttpPost("register/client")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterClient(
        [FromBody] RegisterClientCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(RegisterClient), new { id }, new { id });
    }
```

- [ ] **Step 3: In `Program.cs`, drop the now-unused `RequireClientRole` policy and register the rate limiter**

Replace the `AddAuthorization` block (no controller checks for the `Client` role anymore):

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireBarberRole", policy => policy.RequireClaim("role", "Barber"));
});
```

Add this `using` near the top:

```csharp
using Microsoft.AspNetCore.RateLimiting;
```

Add this block after the CORS policy registration (after the `builder.Services.AddCors(...)` block, before `var app = builder.Build();`):

```csharp
// Anti-spam: cap public appointment creation per client IP. A second,
// phone-number-based check lives in CreateAppointmentCommandHandler since the
// rate limiter's partition key cannot see the deserialized request body.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("appointment-creation", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromHours(1);
        limiterOptions.PermitLimit = 5;
        limiterOptions.QueueLimit = 0;
    });
});
```

Add `app.UseRateLimiter();` right after `app.UseAuthorization();` and before `app.MapControllers();`:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
```

- [ ] **Step 4: Build the whole backend solution**

Run: `cd backend && dotnet build ImperadorBarberShop.sln`
Expected: succeeds with 0 errors. If anything still references `Appointment.Create(Guid clientId, ...)`, `appointment.Accept()`/`.Reject()`, `Review.Create(..., clientId, ...)`, `RegisterClientCommand`, or `GetClientAppointmentsQuery`, fix that call site now — it means an earlier task's file list missed it.

- [ ] **Step 5: Run the full unit test suite**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.UnitTests`
Expected: PASS, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Api/ImperadorBarberShop.Api/Controllers/BarbersController.cs backend/src/Api/ImperadorBarberShop.Api/Controllers/AuthController.cs backend/src/Api/ImperadorBarberShop.Api/Program.cs
git commit -m "feat(api): public slots endpoint, drop client registration, rate-limit appointment creation"
```

---

## Task 12: Integration tests — update for anonymous booking

**Files:**
- Modify: `backend/tests/ImperadorBarberShop.IntegrationTests/Appointments/AppointmentsControllerTests.cs`
- Modify: `backend/tests/ImperadorBarberShop.IntegrationTests/Auth/AuthControllerTests.cs`
- Modify: `backend/tests/ImperadorBarberShop.IntegrationTests/Barbers/BarbersControllerTests.cs`

**Interfaces:**
- Consumes: the full HTTP surface from Tasks 10-11 (`POST /appointments`, `GET/POST /appointments/manage/{token}...`, `PATCH /appointments/{id}/cancel-by-barber`, `GET /barbers/{id}/slots` now public, `POST /auth/register/client` removed).

These require Docker (Testcontainers spins up Postgres) — run with `dotnet test tests/ImperadorBarberShop.IntegrationTests` only if Docker Desktop is running locally; otherwise skip running and rely on Task 11's full-solution build to catch compile errors.

- [ ] **Step 1: Replace `AppointmentsControllerTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

public class AppointmentsControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppointmentsControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private async Task<(string Token, Guid BarberId)> RegisterBarber()
    {
        var email = $"barber-appt-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/barber", new
        {
            name = "Barber",
            email,
            password = "Password123!",
            availability = new[] { new { dayOfWeek = (int)DateTime.UtcNow.AddDays(3).DayOfWeek, startTime = "08:00:00", endTime = "20:00:00" } }
        });
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return (body.GetProperty("accessToken").GetString()!, body.GetProperty("barberId").GetGuid());
    }

    private HttpClient AuthClient(string token)
    {
        var c = _fixture.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task GetBarber_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/appointments/barber");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAppointment_Anonymous_Returns201WithAccessToken()
    {
        var (_, barberId) = await RegisterBarber();

        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(10);
        var payload = new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990000",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.CorteId },
            notes = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/api/v1/appointments", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ManageByToken_AfterCreate_ReturnsAcceptedStatus()
    {
        var (_, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(11);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990001",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = created.GetProperty("accessToken").GetString();

        var response = await _client.GetAsync($"/api/v1/appointments/manage/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("status").GetString().Should().Be("Accepted");
    }

    [Fact]
    public async Task CancelByToken_MoreThan2HoursBeforeSchedule_Returns204()
    {
        var (_, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(12);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990002",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = created.GetProperty("accessToken").GetString();

        var response = await _client.PostAsync($"/api/v1/appointments/manage/{token}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelByBarber_AsBarber_Returns204()
    {
        var (barberToken, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(13);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990003",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var appointmentId = created.GetProperty("id").GetGuid();

        var response = await AuthClient(barberToken)
            .PatchAsync($"/api/v1/appointments/{appointmentId}/cancel-by-barber", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
```

- [ ] **Step 2: Replace `AuthControllerTests.cs`** (drop client registration/login tests, switch the login-flow tests to barber registration)

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Auth;

public class AuthControllerTests : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AuthControllerTests(WebAppFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task RegisterClient_Returns404_RouteNoLongerExists()
    {
        var payload = new { name = "João Teste", email = $"joao-{Guid.NewGuid()}@test.com", password = "Password123!" };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/client", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisterBarber_ValidPayload_Returns201()
    {
        var payload = new
        {
            name = "Carlos Barbeiro",
            email = $"carlos-{Guid.NewGuid()}@test.com",
            password = "Password123!",
            availability = new[]
            {
                new { dayOfWeek = 1, startTime = "09:00:00", endTime = "18:00:00" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/barber", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = $"login-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/barber",
            new { name = "Test Barber", email, password = "Password123!", availability = Array.Empty<object>() });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("role").GetString().Should().Be("Barber");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns403()
    {
        var email = $"wrong-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/barber",
            new { name = "Test", email, password = "Password123!", availability = Array.Empty<object>() });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns403()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "unknown@nowhere.com", password = "anything" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 3: In `BarbersControllerTests.cs`, delete the unused `RegisterAndLogin` helper and fix `GetSlots_WithoutAuth_Returns401`**

Delete the entire `RegisterAndLogin` method (lines 20-42 in the current file) — it's dead code (never called) that registers a "client" role which no longer exists.

Replace `GetSlots_WithoutAuth_Returns401` with:

```csharp
    [Fact]
    public async Task GetSlots_PublicAccess_Returns200()
    {
        var response = await _client.GetAsync(
            $"/api/v1/barbers/{Guid.NewGuid()}/slots?date=2026-04-06&serviceIds={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
```

- [ ] **Step 4: Run the integration tests (requires Docker Desktop running)**

Run: `cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests`
Expected: PASS, 0 failures. If Docker isn't available in this environment, at minimum run `dotnet build tests/ImperadorBarberShop.IntegrationTests` to confirm it compiles.

- [ ] **Step 5: Commit**

```bash
git add backend/tests/ImperadorBarberShop.IntegrationTests/
git commit -m "test: update integration tests for anonymous booking and token-based management"
```

---

## Task 13: Frontend — `types/api.types.ts`

**Files:**
- Modify: `frontend/src/types/api.types.ts`
- Modify: `frontend/src/lib/utils/statusConfig.ts`
- Modify: `frontend/tests/unit/lib/statusConfig.test.ts`

**Interfaces:**
- Produces: `AppointmentStatus = 'Accepted' | 'Cancelled' | 'Completed'`. `Appointment` (drops `clientId`/`hasReview`, adds `clientPhone`). `CreateAppointmentPayload` (drops nothing, adds `clientName`/`clientPhone`). `CreateAppointmentResult`. `AppointmentManage` (the token-page shape). `CreateReviewByTokenPayload`. Removes `RegisterClientPayload`, `CreateReviewPayload` (superseded by `CreateReviewByTokenPayload`).
- Consumes by: Task 14 (api layer), Task 15 (booking form), Task 16 (manage page).
- `statusConfig` is a `Record<AppointmentStatus, StatusConfig>` (`frontend/src/lib/utils/statusConfig.ts`) — it must be updated in lockstep with `AppointmentStatus` below or the project fails to type-check (an object literal assigned to `Record<K, V>` cannot have keys outside `K`).

- [ ] **Step 1: Replace the file**

```typescript
export type UserRole = 'Barber'
export type AppointmentStatus = 'Accepted' | 'Cancelled' | 'Completed'

export interface Service {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  isActive: boolean
}

export type DayOfWeekString =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export interface BarberAvailability {
  dayOfWeek: DayOfWeekString // API returns string enum (JsonStringEnumConverter)
  startTime: string // "HH:mm:ss"
  endTime: string
}

export interface Barber {
  id: string
  userId: string
  name: string
  email: string
  averageRating: number
  availability: BarberAvailability[]
}

export interface ServiceRef {
  id: string
  name: string
  durationMinutes: number
  price: number
}

export interface Appointment {
  id: string
  clientName: string
  clientPhone: string
  barberId: string
  barberName: string
  scheduledAt: string // ISO datetime
  totalDurationMinutes: number
  status: AppointmentStatus
  notes: string | null
  createdAt: string
  services: ServiceRef[]
}

export interface AppointmentManage {
  id: string
  clientName: string
  barberName: string
  scheduledAt: string
  totalDurationMinutes: number
  status: AppointmentStatus
  services: ServiceRef[]
}

export interface Review {
  id: string
  clientName: string
  rating: number
  comment: string | null
  createdAt: string
}

export interface LoginResult {
  accessToken: string
  refreshToken: string
  role: UserRole
  userId: string
  barberId: string | null
}

// Request payload types
export interface LoginPayload {
  email: string
  password: string
}

export interface RegisterBarberPayload {
  name: string
  email: string
  password: string
  availability: BarberAvailability[]
}

export interface CreateAppointmentPayload {
  clientName: string
  clientPhone: string
  barberId: string
  scheduledAt: string
  serviceIds: string[]
  notes?: string
}

export interface CreateAppointmentResult {
  id: string
  accessToken: string
}

export interface CreateReviewByTokenPayload {
  rating: number
  comment?: string
}
```

- [ ] **Step 2: Update `statusConfig.ts`** — drop the `Pending`/`Rejected` entries (the object literal must match the narrowed `AppointmentStatus` exactly)

```typescript
import type { AppointmentStatus } from '@/types/api.types'

export interface StatusConfig {
  label: string
  color: string // Tailwind CSS class
  bgColor: string
}

export const statusConfig: Record<AppointmentStatus, StatusConfig> = {
  Accepted: {
    label: 'Confirmado',
    color: 'text-green-400',
    bgColor: 'bg-green-400/20',
  },
  Cancelled: {
    label: 'Cancelado',
    color: 'text-gray-400',
    bgColor: 'bg-gray-400/20',
  },
  Completed: {
    label: 'Concluído',
    color: 'text-brand-gold',
    bgColor: 'bg-brand-gold/20',
  },
}

export function getStatusConfig(status: AppointmentStatus): StatusConfig {
  return statusConfig[status]
}
```

> Note: `AppointmentCard.test.tsx` (Task 16) asserts `screen.getByText('Aceito')` for a status of `'Accepted'` — that assertion must change to `screen.getByText('Confirmado')` once this label changes. Apply that one-line fix in `AppointmentCard.test.tsx` now, in this task, since it's a direct consequence of this label edit.

- [ ] **Step 3: Replace `statusConfig.test.ts`**

```typescript
import { describe, it, expect } from 'vitest'
import { getStatusConfig, statusConfig } from '@/lib/utils/statusConfig'
import type { AppointmentStatus } from '@/types/api.types'

describe('statusConfig', () => {
  const statuses: AppointmentStatus[] = ['Accepted', 'Cancelled', 'Completed']

  it('has entries for all appointment statuses', () => {
    statuses.forEach((status) => {
      expect(statusConfig[status]).toBeDefined()
    })
  })

  it('each status config has a label, color, and bgColor', () => {
    statuses.forEach((status) => {
      const config = statusConfig[status]
      expect(config.label).toBeTruthy()
      expect(config.color).toBeTruthy()
      expect(config.bgColor).toBeTruthy()
    })
  })

  it('returns correct label for Accepted', () => {
    expect(statusConfig.Accepted.label).toBe('Confirmado')
  })

  it('returns correct label for Cancelled', () => {
    expect(statusConfig.Cancelled.label).toBe('Cancelado')
  })

  it('returns correct label for Completed', () => {
    expect(statusConfig.Completed.label).toBe('Concluído')
  })
})

describe('getStatusConfig', () => {
  it('returns the correct config for a given status', () => {
    const config = getStatusConfig('Accepted')
    expect(config.label).toBe('Confirmado')
  })

  it('is equivalent to direct statusConfig lookup', () => {
    const statuses: AppointmentStatus[] = ['Accepted', 'Cancelled', 'Completed']
    statuses.forEach((status) => {
      expect(getStatusConfig(status)).toEqual(statusConfig[status])
    })
  })
})
```

- [ ] **Step 4: Run the affected tests**

Run: `cd frontend && npm test -- lib/statusConfig`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/src/types/api.types.ts frontend/src/lib/utils/statusConfig.ts frontend/tests/unit/lib/statusConfig.test.ts
git commit -m "feat(types): drop client account types, add token-based appointment shapes"
```

---

## Task 14: Frontend — API layer and `useAppointments` hooks

**Files:**
- Modify: `frontend/src/lib/api/appointments.api.ts`
- Delete: `frontend/src/lib/api/reviews.api.ts`
- Modify: `frontend/src/hooks/useAppointments.ts`
- Modify: `frontend/tests/mocks/handlers.ts`
- Modify: `frontend/tests/unit/hooks/useAppointments.test.tsx`

**Interfaces:**
- Produces: `appointmentsApi.create`, `.getByToken`, `.cancelByToken`, `.reviewByToken`, `.getBarberAppointments`, `.cancelByBarber`, `.complete`. Hooks: `useCreateAppointment`, `useAppointmentByToken`, `useCancelAppointmentByToken`, `useCreateReviewByToken`, `useBarberAppointments`, `useCancelAppointmentByBarber`, `useCompleteAppointment`.
- Consumed by: Task 15 (booking form), Task 16 (manage page), Task 17 (barber dashboard).

- [ ] **Step 1: Replace `appointments.api.ts`**

```typescript
import apiClient from './client'
import type {
  Appointment,
  AppointmentManage,
  CreateAppointmentPayload,
  CreateAppointmentResult,
  CreateReviewByTokenPayload,
  Review,
} from '@/types/api.types'

export const appointmentsApi = {
  create(payload: CreateAppointmentPayload) {
    return apiClient.post<CreateAppointmentResult>('/appointments', payload)
  },

  getByToken(token: string) {
    return apiClient.get<AppointmentManage>(`/appointments/manage/${token}`)
  },

  cancelByToken(token: string) {
    return apiClient.post<void>(`/appointments/manage/${token}/cancel`)
  },

  reviewByToken(token: string, payload: CreateReviewByTokenPayload) {
    return apiClient.post<Review>(`/appointments/manage/${token}/review`, payload)
  },

  getBarberAppointments() {
    return apiClient.get<Appointment[]>('/appointments/barber')
  },

  cancelByBarber(id: string) {
    return apiClient.patch<void>(`/appointments/${id}/cancel-by-barber`)
  },

  complete(id: string) {
    return apiClient.patch<Appointment>(`/appointments/${id}/complete`)
  },
}
```

- [ ] **Step 2: Delete `reviews.api.ts`** (review submission now goes through `appointmentsApi.reviewByToken`)

```bash
git rm frontend/src/lib/api/reviews.api.ts
```

- [ ] **Step 3: Replace `useAppointments.ts`**

```typescript
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { appointmentsApi } from '@/lib/api/appointments.api'
import type { CreateAppointmentPayload, CreateReviewByTokenPayload } from '@/types/api.types'

export function useCreateAppointment() {
  return useMutation({
    mutationFn: (payload: CreateAppointmentPayload) =>
      appointmentsApi.create(payload).then((r) => r.data),
  })
}

export function useAppointmentByToken(token: string) {
  return useQuery({
    queryKey: ['appointments', 'manage', token],
    queryFn: () => appointmentsApi.getByToken(token).then((r) => r.data),
    enabled: !!token,
  })
}

export function useCancelAppointmentByToken(token: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => appointmentsApi.cancelByToken(token),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'manage', token] })
    },
  })
}

export function useCreateReviewByToken(token: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateReviewByTokenPayload) =>
      appointmentsApi.reviewByToken(token, payload).then((r) => r.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'manage', token] })
    },
  })
}

export function useBarberAppointments() {
  return useQuery({
    queryKey: ['appointments', 'barber'],
    queryFn: () => appointmentsApi.getBarberAppointments().then((r) => r.data),
  })
}

export function useCancelAppointmentByBarber() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.cancelByBarber(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['appointments', 'barber'] })
      const previous = queryClient.getQueryData(['appointments', 'barber'])
      queryClient.setQueryData(['appointments', 'barber'], (old: unknown) => {
        if (!Array.isArray(old)) return old
        return old.map((a) => (a.id === id ? { ...a, status: 'Cancelled' } : a))
      })
      return { previous }
    },
    onError: (_err, _id, context) => {
      queryClient.setQueryData(['appointments', 'barber'], context?.previous)
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'barber'] })
    },
  })
}

export function useCompleteAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.complete(id).then((r) => r.data),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['appointments', 'barber'] })
      const previous = queryClient.getQueryData(['appointments', 'barber'])
      queryClient.setQueryData(['appointments', 'barber'], (old: unknown) => {
        if (!Array.isArray(old)) return old
        return old.map((a) => (a.id === id ? { ...a, status: 'Completed' } : a))
      })
      return { previous }
    },
    onError: (_err, _id, context) => {
      queryClient.setQueryData(['appointments', 'barber'], context?.previous)
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'barber'] })
    },
  })
}
```

- [ ] **Step 4: Update `handlers.ts`** — replace the `mockAppointments`/`mockBarberAppointments` fixtures, the `appointments` and `auth`/`reviews` handlers, to match the new shapes

Replace `mockLoginResult`/`mockBarberLoginResult` block with just the barber one:

```typescript
export const mockBarberLoginResult: LoginResult = {
  accessToken: 'mock-barber-access-token',
  refreshToken: 'mock-barber-refresh-token',
  role: 'Barber',
  userId: 'user-barber-1',
  barberId: 'barber-1',
}
```

Replace `mockAppointments` with a single `mockManagedAppointment` (the token-page fixture) and update `mockBarberAppointments` to the new `Appointment` shape (no `clientId`/`hasReview`, add `clientPhone`):

```typescript
export const mockManagedAppointment: AppointmentManage = {
  id: 'appt-1',
  clientName: 'João Silva',
  barberName: 'Carlos Andrade',
  scheduledAt: new Date(Date.now() + 86400000 * 2).toISOString(),
  totalDurationMinutes: 30,
  status: 'Accepted',
  services: [
    { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
  ],
}

export const mockBarberAppointments: Appointment[] = [
  {
    id: 'appt-accepted-1',
    clientName: 'Pedro Costa',
    clientPhone: '+5511999990000',
    barberId: 'barber-1',
    barberName: 'Carlos Andrade',
    scheduledAt: new Date(Date.now() + 86400000).toISOString(),
    totalDurationMinutes: 30,
    status: 'Accepted',
    notes: null,
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
    ],
  },
  {
    id: 'appt-accepted-2',
    clientName: 'Maria Santos',
    clientPhone: '+5511999990001',
    barberId: 'barber-1',
    barberName: 'Carlos Andrade',
    scheduledAt: new Date(Date.now() + 86400000 * 3).toISOString(),
    totalDurationMinutes: 50,
    status: 'Accepted',
    notes: 'Primeira vez aqui',
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-3', name: 'Corte + Barba', durationMinutes: 50, price: 70.0 },
    ],
  },
]
```

Update the `import type` line at the top to add `AppointmentManage` and drop the now-unused `mockLoginResult`. Replace the auth/appointments/reviews handlers:

```typescript
  // Auth
  http.post(`${BASE_URL}/auth/login`, async () => {
    return HttpResponse.json(mockBarberLoginResult)
  }),

  http.post(`${BASE_URL}/auth/register/barber`, async () => {
    return HttpResponse.json(mockBarberLoginResult, { status: 201 })
  }),

  http.post(`${BASE_URL}/auth/refresh`, async () => {
    return HttpResponse.json(mockBarberLoginResult)
  }),

  // Services
  http.get(`${BASE_URL}/services`, () => {
    return HttpResponse.json(mockServices)
  }),

  // Barbers
  http.get(`${BASE_URL}/barbers`, () => {
    return HttpResponse.json(mockBarbers)
  }),

  http.get(`${BASE_URL}/barbers/:id`, ({ params }) => {
    const barber = mockBarbers.find((b) => b.id === params.id)
    if (!barber) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json(barber)
  }),

  http.get(`${BASE_URL}/barbers/:id/slots`, () => {
    return HttpResponse.json(mockSlots)
  }),

  http.get(`${BASE_URL}/barbers/:id/reviews`, () => {
    return HttpResponse.json(mockReviews)
  }),

  http.put(`${BASE_URL}/barbers/me/availability`, async ({ request }) => {
    const body = await request.json()
    return HttpResponse.json({ ...mockBarbers[0], availability: body })
  }),

  // Appointments
  http.post(`${BASE_URL}/appointments`, async () => {
    return HttpResponse.json({ id: 'appt-new-1', accessToken: 'mock-access-token-1' }, { status: 201 })
  }),

  http.get(`${BASE_URL}/appointments/manage/:token`, () => {
    return HttpResponse.json(mockManagedAppointment)
  }),

  http.post(`${BASE_URL}/appointments/manage/:token/cancel`, () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.post(`${BASE_URL}/appointments/manage/:token/review`, async () => {
    return HttpResponse.json(
      { id: 'review-new-1', clientName: 'João Silva', rating: 5, comment: 'Ótimo serviço!', createdAt: new Date().toISOString() },
      { status: 201 }
    )
  }),

  http.get(`${BASE_URL}/appointments/barber`, () => {
    return HttpResponse.json(mockBarberAppointments)
  }),

  http.patch(`${BASE_URL}/appointments/:id/cancel-by-barber`, () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.patch(`${BASE_URL}/appointments/:id/complete`, ({ params }) => {
    const appt = mockBarberAppointments.find((a) => a.id === params.id)
    if (!appt) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({ ...appt, status: 'Completed' })
  }),
```

Remove the old `http.post(.../auth/register/client)`, `http.get(.../appointments/mine)`, `http.delete(.../appointments/:id)`, `http.patch(.../appointments/:id/accept)`, `http.patch(.../appointments/:id/reject)`, and `http.post(.../reviews)` handlers entirely — those routes no longer exist.

- [ ] **Step 5: Replace `useAppointments.test.tsx`**

```typescript
import { describe, it, expect } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import {
  useBarberAppointments,
  useCreateAppointment,
  useAppointmentByToken,
  useCancelAppointmentByToken,
} from '@/hooks/useAppointments'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('useBarberAppointments', () => {
  it('returns barber appointments after loading', async () => {
    const { result } = renderHook(() => useBarberAppointments(), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(2)
  })
})

describe('useCreateAppointment', () => {
  it('creates an appointment and returns its access token', async () => {
    const { result } = renderHook(() => useCreateAppointment(), {
      wrapper: createWrapper(),
    })

    let data: Awaited<ReturnType<typeof result.current.mutateAsync>> | undefined

    await act(async () => {
      data = await result.current.mutateAsync({
        clientName: 'João',
        clientPhone: '+5511999990000',
        barberId: 'barber-1',
        scheduledAt: new Date().toISOString(),
        serviceIds: ['service-1'],
      })
    })

    expect(data?.id).toBeDefined()
    expect(data?.accessToken).toBeDefined()
  })
})

describe('useAppointmentByToken', () => {
  it('returns the managed appointment for a token', async () => {
    const { result } = renderHook(() => useAppointmentByToken('mock-access-token-1'), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.status).toBe('Accepted')
  })
})

describe('useCancelAppointmentByToken', () => {
  it('cancels the appointment for a token', async () => {
    const { result } = renderHook(() => useCancelAppointmentByToken('mock-access-token-1'), {
      wrapper: createWrapper(),
    })

    await act(async () => {
      await result.current.mutateAsync()
    })

    expect(result.current.isSuccess).toBe(true)
  })
})
```

- [ ] **Step 6: Run the affected tests**

Run: `cd frontend && npm test -- hooks/useAppointments`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add frontend/src/lib/api/appointments.api.ts frontend/src/hooks/useAppointments.ts frontend/tests/mocks/handlers.ts frontend/tests/unit/hooks/useAppointments.test.tsx
git rm frontend/src/lib/api/reviews.api.ts
git commit -m "feat(frontend): API layer and hooks for anonymous booking + token-based management"
```

---

## Task 15: Frontend — public booking page (`/agendar`)

**Files:**
- Create: `frontend/src/lib/utils/phone.ts`
- Create: `frontend/tests/unit/lib/phone.test.ts`
- Modify: `frontend/src/components/booking/BookingConfirmation.tsx`
- Create: `frontend/src/app/agendar/page.tsx`
- Delete: `frontend/src/app/client/book/page.tsx`
- Modify: `frontend/tests/e2e/booking.spec.ts`

**Interfaces:**
- Produces: `normalizeBrPhone(raw: string) -> string`, `isValidBrPhone(value: string) -> boolean` (consumed by `BookingConfirmation`). `BookingConfirmationProps` gains `clientName`, `clientPhone`, `onClientNameChange`, `onClientPhoneChange`.
- Consumes: `useCreateAppointment` (Task 14).

- [ ] **Step 1: Write the failing test for the phone util**

```typescript
import { describe, it, expect } from 'vitest'
import { normalizeBrPhone, isValidBrPhone } from '@/lib/utils/phone'

describe('normalizeBrPhone', () => {
  it('strips formatting and prefixes +55', () => {
    expect(normalizeBrPhone('(11) 99999-0000')).toBe('+5511999990000')
  })

  it('is idempotent when already normalized', () => {
    expect(normalizeBrPhone('+5511999990000')).toBe('+5511999990000')
  })
})

describe('isValidBrPhone', () => {
  it('accepts a normalized 11-digit mobile number', () => {
    expect(isValidBrPhone('+5511999990000')).toBe(true)
  })

  it('rejects a number that is too short', () => {
    expect(isValidBrPhone('+551199990000')).toBe(false)
  })

  it('rejects an empty string', () => {
    expect(isValidBrPhone('')).toBe(false)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- lib/phone`
Expected: FAIL (module `@/lib/utils/phone` doesn't exist)

- [ ] **Step 3: Create `phone.ts`**

```typescript
export function normalizeBrPhone(raw: string): string {
  const digits = raw.replace(/\D/g, '')
  const withoutCountryCode = digits.startsWith('55') ? digits.slice(2) : digits
  return `+55${withoutCountryCode}`
}

export function isValidBrPhone(value: string): boolean {
  return /^\+55\d{10,11}$/.test(value)
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npm test -- lib/phone`
Expected: PASS (5 tests)

- [ ] **Step 5: Update `BookingConfirmation.tsx`** to collect name/phone and gate the confirm button

```tsx
'use client'

import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { formatCurrency, formatDateTime, toApiDate } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'
import { isValidBrPhone } from '@/lib/utils/phone'
import type { Barber, Service } from '@/types/api.types'

interface BookingConfirmationProps {
  barber: Barber
  services: Service[]
  selectedDate: Date
  selectedSlot: string
  notes: string
  onNotesChange: (notes: string) => void
  clientName: string
  onClientNameChange: (name: string) => void
  clientPhone: string
  onClientPhoneChange: (phone: string) => void
  onConfirm: () => void
  isLoading: boolean
}

export function BookingConfirmation({
  barber,
  services,
  selectedDate,
  selectedSlot,
  notes,
  onNotesChange,
  clientName,
  onClientNameChange,
  clientPhone,
  onClientPhoneChange,
  onConfirm,
  isLoading,
}: BookingConfirmationProps) {
  const totalDuration = services.reduce((acc, s) => acc + s.durationMinutes, 0)
  const totalPrice = services.reduce((acc, s) => acc + s.price, 0)

  const dateString = toApiDate(selectedDate) // "YYYY-MM-DD"
  const scheduledAt = new Date(`${dateString}T${selectedSlot}Z`)

  const canConfirm = clientName.trim().length > 0 && isValidBrPhone(clientPhone)

  return (
    <div className="flex flex-col gap-6">
      {/* Contact info */}
      <div className="flex flex-col gap-3">
        <Input
          label="Nome completo"
          value={clientName}
          onChange={(e) => onClientNameChange(e.target.value)}
          placeholder="Seu nome"
        />
        <Input
          label="WhatsApp"
          value={clientPhone}
          onChange={(e) => onClientPhoneChange(e.target.value)}
          placeholder="+55 11 99999-0000"
        />
      </div>

      {/* Summary card */}
      <div className="rounded-xl border border-brand-gold/30 bg-brand-black-soft p-6">
        <h3 className="font-montserrat font-bold text-brand-white mb-4">
          Resumo do agendamento
        </h3>

        <div className="flex flex-col gap-3">
          <div className="flex justify-between">
            <span className="text-brand-white/60 text-sm">Barbeiro</span>
            <span className="text-brand-white font-medium">{barber.name}</span>
          </div>

          <div className="border-t border-brand-white/10 pt-3">
            <span className="text-brand-white/60 text-sm block mb-2">Serviços</span>
            {services.map((s) => (
              <div key={s.id} className="flex justify-between text-sm py-1">
                <span className="text-brand-white">{s.name}</span>
                <span className="text-brand-gold">{formatCurrency(s.price)}</span>
              </div>
            ))}
          </div>

          <div className="border-t border-brand-white/10 pt-3 flex justify-between">
            <span className="text-brand-white/60 text-sm">Duração total</span>
            <span className="text-brand-white font-medium">{formatDuration(totalDuration)}</span>
          </div>

          <div className="flex justify-between">
            <span className="text-brand-white/60 text-sm">Data e horário</span>
            <span className="text-brand-white font-medium">
              {formatDateTime(scheduledAt.toISOString())}
            </span>
          </div>

          <div className="border-t border-brand-gold/30 pt-3 flex justify-between">
            <span className="font-montserrat font-semibold text-brand-white">Total</span>
            <span className="font-montserrat font-bold text-brand-gold text-xl">
              {formatCurrency(totalPrice)}
            </span>
          </div>
        </div>
      </div>

      {/* Notes */}
      <div className="flex flex-col gap-1">
        <label htmlFor="booking-notes" className="text-sm font-medium text-brand-white/80">
          Observações (opcional)
        </label>
        <textarea
          id="booking-notes"
          value={notes}
          onChange={(e) => onNotesChange(e.target.value)}
          placeholder="Ex: Prefiro o corte mais curto nas laterais..."
          rows={3}
          className="w-full rounded-md border border-brand-white/20 bg-brand-black-soft px-3 py-2.5 text-brand-white placeholder:text-brand-white/30 focus:border-brand-gold focus:outline-none focus:ring-1 focus:ring-brand-gold resize-none"
        />
      </div>

      <Button onClick={onConfirm} isLoading={isLoading} disabled={!canConfirm} size="lg" className="w-full">
        Confirmar Agendamento
      </Button>
    </div>
  )
}
```

- [ ] **Step 6: Create `app/agendar/page.tsx`** (public booking wizard — replaces `app/client/book/page.tsx`)

```tsx
'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { BarberPicker } from '@/components/booking/BarberPicker'
import { ServicePicker } from '@/components/booking/ServicePicker'
import { SlotPicker } from '@/components/booking/SlotPicker'
import { BookingConfirmation } from '@/components/booking/BookingConfirmation'
import { Button } from '@/components/ui/Button'
import { useCreateAppointment } from '@/hooks/useAppointments'
import { useServices } from '@/hooks/useServices'
import { normalizeBrPhone } from '@/lib/utils/phone'
import { toApiDate } from '@/lib/utils/formatDateTime'
import type { Barber, Service } from '@/types/api.types'

type Step = 1 | 2 | 3 | 4

const STEP_LABELS = ['Barbeiro', 'Serviços', 'Data e Horário', 'Confirmar']

export default function AgendarPage() {
  const router = useRouter()
  const [step, setStep] = useState<Step>(1)

  const [selectedBarber, setSelectedBarber] = useState<Barber | null>(null)
  const [selectedServiceIds, setSelectedServiceIds] = useState<string[]>([])
  const [selectedDate, setSelectedDate] = useState<Date | null>(null)
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null)
  const [notes, setNotes] = useState('')
  const [clientName, setClientName] = useState('')
  const [clientPhone, setClientPhone] = useState('')

  const { data: allServices } = useServices()
  const createAppointment = useCreateAppointment()

  function toggleService(service: Service) {
    setSelectedServiceIds((prev) =>
      prev.includes(service.id) ? prev.filter((id) => id !== service.id) : [...prev, service.id]
    )
  }

  function canAdvance(): boolean {
    if (step === 1) return !!selectedBarber
    if (step === 2) return selectedServiceIds.length > 0
    if (step === 3) return !!selectedDate && !!selectedSlot
    return true
  }

  function handleNext() {
    if (step < 4) setStep((step + 1) as Step)
  }

  function handleBack() {
    if (step > 1) setStep((step - 1) as Step)
  }

  async function handleConfirm() {
    if (!selectedBarber || !selectedDate || !selectedSlot) return

    const dateString = toApiDate(selectedDate)
    const scheduledAt = new Date(`${dateString}T${selectedSlot}Z`)

    try {
      const result = await createAppointment.mutateAsync({
        clientName: clientName.trim(),
        clientPhone: normalizeBrPhone(clientPhone),
        barberId: selectedBarber.id,
        scheduledAt: scheduledAt.toISOString(),
        serviceIds: selectedServiceIds,
        notes: notes.trim() || undefined,
      })
      router.push(`/agendamento/${result.accessToken}`)
    } catch {
      // Error handled by mutation state
    }
  }

  const selectedServices = allServices?.filter((s) => selectedServiceIds.includes(s.id)) ?? []

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
      <div className="mb-8">
        <h1 className="font-montserrat text-2xl font-black text-brand-white">Novo Agendamento</h1>
        <p className="mt-1 text-sm text-brand-white/50">Siga os passos para agendar seu atendimento</p>
      </div>

      <nav aria-label="Progresso do agendamento" className="mb-8">
        <ol className="flex items-center gap-0">
          {STEP_LABELS.map((label, idx) => {
            const stepNum = (idx + 1) as Step
            const isCompleted = stepNum < step
            const isCurrent = stepNum === step

            return (
              <li key={label} className="flex items-center flex-1">
                <div className="flex flex-col items-center gap-1">
                  <div
                    className={[
                      'flex h-8 w-8 items-center justify-center rounded-full text-sm font-bold transition-colors',
                      isCompleted
                        ? 'bg-brand-gold text-brand-black'
                        : isCurrent
                        ? 'border-2 border-brand-gold text-brand-gold'
                        : 'border-2 border-brand-white/20 text-brand-white/30',
                    ].join(' ')}
                    aria-current={isCurrent ? 'step' : undefined}
                  >
                    {isCompleted ? (
                      <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                        <path
                          fillRule="evenodd"
                          d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                          clipRule="evenodd"
                        />
                      </svg>
                    ) : (
                      stepNum
                    )}
                  </div>
                  <span
                    className={[
                      'hidden sm:block text-xs font-medium',
                      isCurrent ? 'text-brand-gold' : isCompleted ? 'text-brand-gold/60' : 'text-brand-white/30',
                    ].join(' ')}
                  >
                    {label}
                  </span>
                </div>
                {idx < STEP_LABELS.length - 1 && (
                  <div
                    className={[
                      'flex-1 h-px mx-2 transition-colors',
                      isCompleted ? 'bg-brand-gold' : 'bg-brand-white/10',
                    ].join(' ')}
                    aria-hidden="true"
                  />
                )}
              </li>
            )
          })}
        </ol>
      </nav>

      <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6 mb-6">
        <h2 className="font-montserrat font-bold text-brand-white mb-6">
          {step === 1 && 'Escolha o Barbeiro'}
          {step === 2 && 'Escolha os Serviços'}
          {step === 3 && 'Escolha Data e Horário'}
          {step === 4 && 'Confirmar Agendamento'}
        </h2>

        {step === 1 && (
          <BarberPicker
            selectedBarberId={selectedBarber?.id ?? null}
            onSelect={(barber) => {
              setSelectedBarber(barber)
              setSelectedSlot(null)
            }}
          />
        )}

        {step === 2 && <ServicePicker selectedServiceIds={selectedServiceIds} onToggle={toggleService} />}

        {step === 3 && selectedBarber && (
          <SlotPicker
            barberId={selectedBarber.id}
            serviceIds={selectedServiceIds}
            barberAvailability={selectedBarber.availability}
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            onDateChange={(d) => {
              setSelectedDate(d)
              setSelectedSlot(null)
            }}
            onSlotChange={setSelectedSlot}
          />
        )}

        {step === 4 && selectedBarber && selectedDate && selectedSlot && (
          <BookingConfirmation
            barber={selectedBarber}
            services={selectedServices}
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            notes={notes}
            onNotesChange={setNotes}
            clientName={clientName}
            onClientNameChange={setClientName}
            clientPhone={clientPhone}
            onClientPhoneChange={setClientPhone}
            onConfirm={handleConfirm}
            isLoading={createAppointment.isPending}
          />
        )}

        {createAppointment.isError && (
          <p role="alert" className="mt-4 text-sm text-red-400">
            Erro ao criar agendamento. Tente novamente.
          </p>
        )}
      </div>

      {step < 4 && (
        <div className="flex justify-between">
          <Button variant="ghost" onClick={handleBack} disabled={step === 1}>
            Voltar
          </Button>
          <Button onClick={handleNext} disabled={!canAdvance()}>
            Próximo
          </Button>
        </div>
      )}
      {step === 4 && (
        <div className="flex justify-start">
          <Button variant="ghost" onClick={handleBack}>
            Voltar
          </Button>
        </div>
      )}
    </div>
  )
}
```

- [ ] **Step 7: Delete the old client booking page**

```bash
git rm frontend/src/app/client/book/page.tsx
```

- [ ] **Step 8: Replace `tests/e2e/booking.spec.ts`**

```typescript
import { test, expect } from '@playwright/test'

test.describe('Fluxo de agendamento', () => {
  test('realiza um agendamento completo sem login', async ({ page }) => {
    await page.goto('/agendar')
    await expect(page.getByRole('heading', { name: /Novo Agendamento/i })).toBeVisible()

    // ─── Passo 1: Escolher barbeiro ───────────────────────────────────
    await expect(page.getByText(/Escolha o Barbeiro/i)).toBeVisible()
    const barberCards = page.getByRole('listitem')
    await expect(barberCards.first()).toBeVisible({ timeout: 10000 })
    await barberCards.first().click()
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 2: Escolher serviços ───────────────────────────────────
    await expect(page.getByText(/Escolha os Serviços/i)).toBeVisible()
    const serviceCheckboxes = page.locator('input[type="checkbox"]')
    await expect(serviceCheckboxes.first()).toBeVisible({ timeout: 10000 })
    await serviceCheckboxes.first().click()
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 3: Escolher data e horário ─────────────────────────────
    await expect(page.getByText(/Escolha Data e Horário/i)).toBeVisible()
    const dayButtons = page.locator('.rdp-root button:not([disabled])')
    await expect(dayButtons.first()).toBeVisible({ timeout: 10000 })
    await dayButtons.first().click()
    const slotButtons = page.getByRole('option')
    await expect(slotButtons.first()).toBeVisible({ timeout: 10000 })
    await slotButtons.first().click()
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 4: Confirmar ───────────────────────────────────────────
    await expect(page.getByText(/Confirmar Agendamento/i)).toBeVisible()
    await page.getByLabel('Nome completo').fill('João Teste')
    await page.getByLabel('WhatsApp').fill('11999990000')

    await page.getByRole('button', { name: /Confirmar Agendamento/i }).click()

    // Redirects to the public management page, keyed by the access token
    await expect(page).toHaveURL(/\/agendamento\/.+/, { timeout: 15000 })
  })

  test('o botão Próximo fica desabilitado até um barbeiro ser selecionado', async ({ page }) => {
    await page.goto('/agendar')

    const nextButton = page.getByRole('button', { name: /Próximo/i })
    await expect(nextButton).toBeDisabled()
  })
})
```

- [ ] **Step 9: Run the affected unit tests**

Run: `cd frontend && npm test -- lib/phone`
Expected: PASS

- [ ] **Step 10: Commit**

```bash
git add frontend/src/lib/utils/phone.ts frontend/tests/unit/lib/phone.test.ts frontend/src/components/booking/BookingConfirmation.tsx frontend/src/app/agendar/page.tsx frontend/tests/e2e/booking.spec.ts
git rm frontend/src/app/client/book/page.tsx
git commit -m "feat(frontend): public /agendar booking wizard with name/WhatsApp capture"
```

---

## Task 16: Frontend — public management page (`/agendamento/[token]`)

**Files:**
- Modify: `frontend/src/components/appointments/ReviewForm.tsx`
- Modify: `frontend/tests/unit/components/appointments/ReviewForm.test.tsx`
- Create: `frontend/src/app/agendamento/[token]/page.tsx`
- Create: `frontend/tests/unit/app/agendamento/page.test.tsx`
- Modify: `frontend/tests/unit/components/appointments/AppointmentCard.test.tsx`

**Interfaces:**
- Consumes: `useAppointmentByToken`, `useCancelAppointmentByToken`, `useCreateReviewByToken` (Task 14), `appointmentsApi.reviewByToken` (Task 14).
- Produces: `ReviewForm` now takes `{ accessToken: string; onSuccess: () => void }` instead of `{ appointmentId; onSuccess }`.

- [ ] **Step 1: Replace `ReviewForm.tsx`** (swap `appointmentId` for `accessToken`, post through the token endpoint)

```tsx
'use client'

import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { StarRatingInput } from '@/components/ui/StarRating'
import { appointmentsApi } from '@/lib/api/appointments.api'

interface ReviewFormProps {
  accessToken: string
  onSuccess: () => void
}

export function ReviewForm({ accessToken, onSuccess }: ReviewFormProps) {
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (rating === 0) {
      setError('Selecione uma avaliação de 1 a 5 estrelas.')
      return
    }
    setError(null)
    setIsSubmitting(true)
    try {
      await appointmentsApi.reviewByToken(accessToken, {
        rating,
        comment: comment.trim() || undefined,
      })
      onSuccess()
    } catch {
      setError('Erro ao enviar avaliação. Tente novamente.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-col items-center gap-2">
        <p className="text-sm text-brand-white/70">Como você avalia o serviço?</p>
        <StarRatingInput value={rating} onChange={setRating} />
        {rating > 0 && (
          <span className="text-xs text-brand-gold">
            {['', 'Péssimo', 'Ruim', 'Regular', 'Bom', 'Excelente'][rating]}
          </span>
        )}
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="review-comment" className="text-sm font-medium text-brand-white/80">
          Comentário (opcional)
        </label>
        <textarea
          id="review-comment"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          placeholder="Conte como foi sua experiência..."
          rows={3}
          className="w-full rounded-md border border-brand-white/20 bg-brand-black px-3 py-2.5 text-brand-white placeholder:text-brand-white/30 focus:border-brand-gold focus:outline-none focus:ring-1 focus:ring-brand-gold resize-none"
        />
      </div>

      {error && (
        <p role="alert" className="text-sm text-red-400">
          {error}
        </p>
      )}

      <Button type="submit" isLoading={isSubmitting} className="w-full">
        Enviar avaliação
      </Button>
    </form>
  )
}
```

- [ ] **Step 2: Update `ReviewForm.test.tsx`** to pass `accessToken` instead of `appointmentId`

Replace every `appointmentId="appt-1"` prop in the file with `accessToken="mock-access-token-1"` (5 occurrences — one per `render(<ReviewForm .../>)` call). No other change needed; the MSW handler for `POST /appointments/manage/:token/review` from Task 14 covers the same success path the old `/reviews` handler did.

- [ ] **Step 3: Create `app/agendamento/[token]/page.tsx`**

```tsx
'use client'

import { useParams } from 'next/navigation'
import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import { Badge } from '@/components/ui/Badge'
import { ReviewForm } from '@/components/appointments/ReviewForm'
import { useAppointmentByToken, useCancelAppointmentByToken } from '@/hooks/useAppointments'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'

export default function ManageAppointmentPage() {
  const params = useParams<{ token: string }>()
  const token = params.token
  const { data: appointment, isLoading, isError } = useAppointmentByToken(token)
  const cancelAppointment = useCancelAppointmentByToken(token)
  const [reviewSubmitted, setReviewSubmitted] = useState(false)

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError || !appointment) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-10 text-center">
        <p role="alert" className="text-red-400">
          Agendamento não encontrado. Verifique o link recebido.
        </p>
      </div>
    )
  }

  const totalPrice = appointment.services.reduce((acc, s) => acc + s.price, 0)
  const canCancel =
    appointment.status === 'Accepted' &&
    new Date(appointment.scheduledAt).getTime() - Date.now() > 2 * 60 * 60 * 1000

  return (
    <div className="mx-auto max-w-2xl px-4 py-10 sm:px-6">
      <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6 flex flex-col gap-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h1 className="font-montserrat text-xl font-bold text-brand-white">
              {appointment.barberName}
            </h1>
            <p className="text-sm text-brand-white/50">{appointment.clientName}</p>
          </div>
          <Badge status={appointment.status} />
        </div>

        <div className="flex flex-wrap gap-1">
          {appointment.services.map((s) => (
            <span key={s.id} className="rounded-full bg-brand-white/10 px-2.5 py-0.5 text-xs text-brand-white/70">
              {s.name}
            </span>
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-4 text-sm text-brand-white/60">
          <span>{formatDateTime(appointment.scheduledAt)}</span>
          <span>{formatDuration(appointment.totalDurationMinutes)}</span>
          <span className="font-semibold text-brand-gold">{formatCurrency(totalPrice)}</span>
        </div>

        {appointment.status === 'Accepted' && (
          <Button
            variant="danger"
            isLoading={cancelAppointment.isPending}
            disabled={!canCancel}
            onClick={() => cancelAppointment.mutate()}
          >
            {canCancel ? 'Cancelar agendamento' : 'Cancelamento indisponível (menos de 2h)'}
          </Button>
        )}

        {appointment.status === 'Cancelled' && (
          <p className="text-sm text-brand-white/60">Este agendamento foi cancelado.</p>
        )}

        {appointment.status === 'Completed' && !reviewSubmitted && (
          <ReviewForm accessToken={token} onSuccess={() => setReviewSubmitted(true)} />
        )}

        {appointment.status === 'Completed' && reviewSubmitted && (
          <p className="text-sm text-brand-gold">Obrigado pela sua avaliação!</p>
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Write the failing test for the page**

```tsx
import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../../../test-utils'
import { useParams } from 'next/navigation'
import { vi } from 'vitest'
import ManageAppointmentPage from '@/app/agendamento/[token]/page'

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
}))

describe('ManageAppointmentPage', () => {
  it('renders the appointment summary for a valid token', async () => {
    vi.mocked(useParams).mockReturnValue({ token: 'mock-access-token-1' })
    render(<ManageAppointmentPage />)

    await waitFor(() => {
      expect(screen.getByText('Carlos Andrade')).toBeInTheDocument()
      expect(screen.getByText('João Silva')).toBeInTheDocument()
    })
  })

  it('shows a cancel button for an Accepted appointment scheduled far in the future', async () => {
    vi.mocked(useParams).mockReturnValue({ token: 'mock-access-token-1' })
    render(<ManageAppointmentPage />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Cancelar agendamento' })).toBeEnabled()
    })
  })
})
```

> Note: this test relies on `mockManagedAppointment` from `tests/mocks/handlers.ts` (Task 14), which is scheduled 2 days in the future and has `status: 'Accepted'` — matching the two assertions above.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npm test -- app/agendamento`
Expected: PASS (2 tests). If it fails because `next/navigation`'s `useParams` mock doesn't match how Next 15's App Router resolves dynamic params in a client component under test, fall back to rendering the inner content directly (extract the body of `ManageAppointmentPage` into an exported `ManageAppointmentView({ token }: { token: string })` and test that instead, calling it from the page as `<ManageAppointmentView token={useParams<{ token: string }>().token} />`).

- [ ] **Step 6: Update `AppointmentCard.test.tsx`** fixture to match the new `Appointment` type (no `clientId`/`hasReview`, add `clientPhone`)

```typescript
const mockAppointment: Appointment = {
  id: 'appt-1',
  clientName: 'João Silva',
  clientPhone: '+5511999990000',
  barberId: 'barber-1',
  barberName: 'Carlos Andrade',
  scheduledAt: '2024-06-15T14:30:00.000Z',
  totalDurationMinutes: 30,
  status: 'Accepted',
  notes: 'Manter a lateral curta',
  createdAt: '2024-06-10T10:00:00.000Z',
  services: [
    { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
    { id: 'service-2', name: 'Barba', durationMinutes: 20, price: 35.0 },
  ],
}
```

Also update the `'displays the status badge'` test below it — the label changed from `'Aceito'` to `'Confirmado'` in Task 13's `statusConfig.ts` update:

```typescript
  it('displays the status badge', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText('Confirmado')).toBeInTheDocument()
  })
```

(Every other test body in the file is unchanged, since `AppointmentCard.tsx` itself never read `clientId`/`hasReview` directly.)

- [ ] **Step 7: Run the full frontend unit suite**

Run: `cd frontend && npm test`
Expected: PASS — this is the first point where every previously-broken test file (Tasks 13-16's changes) gets checked together.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/components/appointments/ReviewForm.tsx frontend/tests/unit/components/appointments/ReviewForm.test.tsx frontend/src/app/agendamento/ frontend/tests/unit/app/agendamento/ frontend/tests/unit/components/appointments/AppointmentCard.test.tsx
git commit -m "feat(frontend): public /agendamento/[token] page for cancel + post-completion review"
```

---

## Task 17: Frontend — remove client accounts everywhere else, finish barber dashboard

**Files:**
- Modify: `frontend/src/lib/api/auth.api.ts`
- Modify: `frontend/src/components/auth/LoginForm.tsx`
- Modify: `frontend/src/middleware.ts`
- Modify: `frontend/src/components/layout/Header.tsx`
- Modify: `frontend/src/app/page.tsx`
- Modify: `frontend/src/app/(auth)/login/page.tsx`
- Modify: `frontend/src/components/appointments/AppointmentCard.tsx`
- Modify: `frontend/tests/unit/components/appointments/AppointmentCard.test.tsx`
- Modify: `frontend/src/components/appointments/BarberAppointmentList.tsx`
- Modify: `frontend/tests/unit/components/appointments/BarberAppointmentList.test.tsx`
- Delete: `frontend/src/app/client/dashboard/page.tsx`, `frontend/src/app/client/layout.tsx`
- Delete: `frontend/src/app/(auth)/register/client/page.tsx`, `frontend/src/components/auth/ClientRegisterForm.tsx`
- Delete: `frontend/src/components/appointments/ClientAppointmentList.tsx`, `frontend/tests/unit/components/appointments/ClientAppointmentList.test.tsx`
- Modify: `frontend/tests/e2e/auth-redirect.spec.ts`
- Modify: `frontend/tests/e2e/barber-acceptance.spec.ts`

**Interfaces:**
- Consumes: `useCancelAppointmentByBarber`, `useCompleteAppointment` (Task 14).
- Final task in the plan — leaves the frontend with zero references to client accounts.

- [ ] **Step 1: `auth.api.ts`** — remove `registerClient` and the `RegisterClientPayload` import

```typescript
import apiClient from './client'
import type { LoginPayload, LoginResult, RegisterBarberPayload } from '@/types/api.types'

export interface RegisterResult {
  id: string
}

export const authApi = {
  login(payload: LoginPayload) {
    return apiClient.post<LoginResult>('/auth/login', payload)
  },

  registerBarber(payload: RegisterBarberPayload) {
    return apiClient.post<RegisterResult>('/auth/register/barber', payload)
  },

  refresh(userId: string, refreshToken: string) {
    return apiClient.post<LoginResult>('/auth/refresh', { userId, refreshToken })
  },
}
```

- [ ] **Step 2: `LoginForm.tsx`** — login is barber-only now, always redirect to the barber dashboard

```tsx
  async function onSubmit(data: LoginFormData) {
    setServerError(null)
    try {
      const res = await authApi.login(data)
      login(res.data)
      router.push('/barber/dashboard')
    } catch {
      setServerError('E-mail ou senha incorretos. Tente novamente.')
    }
  }
```

(Replace just this function body inside the existing `LoginForm.tsx` — everything else in the file is unchanged.)

- [ ] **Step 3: `middleware.ts`** — drop the `/client` protection, keep `/barber`

```typescript
import { NextResponse, type NextRequest } from 'next/server'

const ROLE_COOKIE = 'imperador_access_role'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  const role = request.cookies.get(ROLE_COOKIE)?.value

  if (pathname.startsWith('/barber')) {
    if (!role || role !== 'Barber') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/barber/:path*'],
}
```

- [ ] **Step 4: `Header.tsx`** — public "Agendar" link for everyone, barber-only dashboard link, no client registration

```tsx
'use client'

import Link from 'next/link'
import { useAuthContext } from '@/providers/AuthProvider'
import { Button } from '@/components/ui/Button'
import { useRouter } from 'next/navigation'

export function Header() {
  const { user, logout } = useAuthContext()
  const router = useRouter()

  function handleLogout() {
    logout()
    router.push('/')
  }

  return (
    <header className="sticky top-0 z-40 border-b border-brand-white/10 bg-brand-black/95 backdrop-blur-sm">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6">
        <Link href="/" className="flex flex-col leading-none group">
          <span className="font-montserrat text-xl font-black tracking-widest text-brand-gold group-hover:text-brand-gold-light transition-colors">
            O IMPERADOR
          </span>
          <span className="font-montserrat text-[0.55rem] tracking-[0.35em] text-brand-gold/60 group-hover:text-brand-gold/80 transition-colors">
            BARBER SHOP
          </span>
        </Link>

        <nav className="flex items-center gap-3">
          <Link href="/agendar">
            <Button variant="primary" size="sm">
              Agendar
            </Button>
          </Link>
          {!user ? (
            <Link href="/login">
              <Button variant="ghost" size="sm">
                Entrar
              </Button>
            </Link>
          ) : (
            <>
              {user.role === 'Barber' && (
                <Link href="/barber/dashboard">
                  <Button variant="ghost" size="sm">
                    Minha Agenda
                  </Button>
                </Link>
              )}
              <Button variant="secondary" size="sm" onClick={handleLogout}>
                Sair
              </Button>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
```

- [ ] **Step 5: `app/page.tsx`** — point booking CTAs at `/agendar`, drop client registration CTAs

Replace the hero CTA block:

```tsx
          <div className="flex flex-col gap-3 sm:flex-row mt-2">
            <Link href="/agendar">
              <Button size="lg" className="min-w-[200px]">
                Agendar agora
              </Button>
            </Link>
          </div>
```

Replace the final CTA section's buttons:

```tsx
          <div className="flex flex-col gap-3 sm:flex-row justify-center">
            <Link href="/agendar">
              <Button size="lg">Agendar agora</Button>
            </Link>
            <Link href="/register/barber">
              <Button variant="secondary" size="lg">
                Sou barbeiro
              </Button>
            </Link>
          </div>
```

(Also update the surrounding copy "Cadastre-se gratuitamente e agende seu primeiro corte hoje mesmo." → "Agende seu primeiro corte hoje mesmo, sem cadastro." since there's no longer a client account to create.)

- [ ] **Step 6: `app/(auth)/login/page.tsx`** — remove the client sign-up link (login is for barbers only)

```tsx
        <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-8">
          <LoginForm />
        </div>
```

(Delete the trailing `<p>Não tem uma conta? ...</p>` block that linked to `/register/client`.)

- [ ] **Step 7: `AppointmentCard.tsx`** — show the client's phone alongside their name (barber must always see who's coming and how to reach them)

```tsx
          <p className="text-sm text-brand-white/50">
            Cliente: {appointment.clientName} · {appointment.clientPhone}
          </p>
```

(Replace just the existing `<p className="text-sm text-brand-white/50">Cliente: {appointment.clientName}</p>` line.)

- [ ] **Step 8: Add one assertion to `AppointmentCard.test.tsx`** (the fixture already has `clientPhone` from Task 16)

```typescript
  it('displays the client phone', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText(/\+5511999990000/)).toBeInTheDocument()
  })
```

(Add this as a new `it` block, anywhere after the existing `'displays the client name'` test.)

- [ ] **Step 9: Replace `BarberAppointmentList.tsx`** — no more Pending/Aceitar/Recusar; `Cancelar` (barber-initiated) and `Concluir` are both available on an `Accepted` appointment

```tsx
'use client'

import { AppointmentCard } from './AppointmentCard'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import {
  useBarberAppointments,
  useCancelAppointmentByBarber,
  useCompleteAppointment,
} from '@/hooks/useAppointments'

export function BarberAppointmentList() {
  const { data: appointments, isLoading, isError } = useBarberAppointments()
  const cancel = useCancelAppointmentByBarber()
  const complete = useCompleteAppointment()

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError) {
    return (
      <p role="alert" className="text-center text-red-400 py-8">
        Erro ao carregar agendamentos.
      </p>
    )
  }

  if (!appointments || appointments.length === 0) {
    return (
      <p className="text-center text-brand-white/50 py-8">
        Nenhum agendamento encontrado.
      </p>
    )
  }

  const sorted = [...appointments].sort(
    (a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()
  )

  return (
    <div className="flex flex-col gap-3">
      {sorted.map((appointment) => (
        <AppointmentCard
          key={appointment.id}
          appointment={appointment}
          actions={
            appointment.status === 'Accepted' ? (
              <>
                <Button
                  variant="secondary"
                  size="sm"
                  isLoading={complete.isPending && complete.variables === appointment.id}
                  onClick={() => complete.mutate(appointment.id)}
                >
                  Concluir
                </Button>
                <Button
                  variant="danger"
                  size="sm"
                  isLoading={cancel.isPending && cancel.variables === appointment.id}
                  onClick={() => cancel.mutate(appointment.id)}
                >
                  Cancelar
                </Button>
              </>
            ) : undefined
          }
        />
      ))}
    </div>
  )
}
```

- [ ] **Step 10: Replace `BarberAppointmentList.test.tsx`**

```tsx
import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../../test-utils'
import { BarberAppointmentList } from '@/components/appointments/BarberAppointmentList'

describe('BarberAppointmentList', () => {
  it('shows loading spinner initially', () => {
    render(<BarberAppointmentList />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders appointments after loading', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getByText(/Pedro Costa/)).toBeInTheDocument()
      expect(screen.getByText(/Maria Santos/)).toBeInTheDocument()
    })
  })

  it('shows Concluir button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Concluir/i })).toHaveLength(2)
    })
  })

  it('shows Cancelar button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Cancelar/i })).toHaveLength(2)
    })
  })
})
```

- [ ] **Step 11: Delete the client-only pages and components**

```bash
git rm -r frontend/src/app/client
git rm frontend/src/app/\(auth\)/register/client/page.tsx
git rm frontend/src/components/auth/ClientRegisterForm.tsx
git rm frontend/src/components/appointments/ClientAppointmentList.tsx
git rm frontend/tests/unit/components/appointments/ClientAppointmentList.test.tsx
```

- [ ] **Step 12: Replace `tests/e2e/auth-redirect.spec.ts`**

```typescript
import { test, expect } from '@playwright/test'

test.describe('Redirecionamento de autenticação', () => {
  test('redireciona para /login ao visitar /barber/dashboard sem autenticação', async ({ page }) => {
    await page.context().clearCookies()
    await page.goto('/barber/dashboard')
    await expect(page).toHaveURL(/\/login/)
  })

  test('a página de login é acessível sem autenticação', async ({ page }) => {
    await page.goto('/login')
    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByRole('heading', { name: /Bem-vindo/i })).toBeVisible()
  })

  test('a landing page é acessível sem autenticação', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL('/')
    await expect(page.getByRole('heading', { name: /IMPERADOR/i })).toBeVisible()
  })

  test('a página /agendar é acessível sem autenticação', async ({ page }) => {
    await page.goto('/agendar')
    await expect(page).toHaveURL(/\/agendar/)
    await expect(page.getByRole('heading', { name: /Novo Agendamento/i })).toBeVisible()
  })

  test('redireciona com parâmetro de redirect na URL', async ({ page }) => {
    await page.context().clearCookies()
    await page.goto('/barber/dashboard')
    await expect(page).toHaveURL(/redirect=%2Fbarber%2Fdashboard/)
  })
})
```

- [ ] **Step 13: Replace `tests/e2e/barber-acceptance.spec.ts`** (drop Aceitar/Recusar scenarios — appointments are auto-confirmed now; add a barber-cancel scenario)

```typescript
import { test, expect } from '@playwright/test'

test.describe('Gestão de agendamentos pelo barbeiro', () => {
  test.beforeEach(async ({ page }) => {
    await page.context().clearCookies()
    await page.evaluate(() => localStorage.clear())
  })

  test('registra um barbeiro com disponibilidade', async ({ page }) => {
    const timestamp = Date.now()
    const email = `barbeiro${timestamp}@teste.com`

    await page.goto('/register/barber')
    await expect(page.getByRole('heading', { name: /Cadastro de Barbeiro/i })).toBeVisible()

    await page.getByLabel('Nome completo').fill('Barbeiro Teste')
    await page.getByLabel('E-mail').fill(email)

    const passwordFields = page.getByLabel(/Senha/)
    await passwordFields.first().fill('senha123')
    await passwordFields.last().fill('senha123')

    await expect(page.getByRole('group', { name: /Disponibilidade/i })).toBeVisible()
    await page.getByRole('button', { name: /Criar conta de barbeiro/i }).click()

    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 10000 })
  })

  test('faz login como barbeiro e visualiza o dashboard', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-mail').fill('barber@test.com')
    await page.getByLabel('Senha').fill('senha123')
    await page.getByRole('button', { name: /Entrar/i }).click()

    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 10000 })
    await expect(page.getByRole('heading', { name: /Minha Agenda/i })).toBeVisible()
  })

  test('barbeiro cancela um agendamento confirmado, se existir', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-mail').fill('barber@test.com')
    await page.getByLabel('Senha').fill('senha123')
    await page.getByRole('button', { name: /Entrar/i }).click()
    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 10000 })

    const cancelButton = page.getByRole('button', { name: /Cancelar/i }).first()
    const hasAppointment = await cancelButton.isVisible({ timeout: 5000 }).catch(() => false)

    if (hasAppointment) {
      await cancelButton.click()
      await page.waitForTimeout(1000)
    } else {
      test.info().annotations.push({
        type: 'info',
        description: 'Nenhum agendamento confirmado encontrado. Teste ignorado.',
      })
    }
  })
})
```

- [ ] **Step 14: Run the full frontend unit suite and type-check**

Run: `cd frontend && npm test && npx tsc --noEmit`
Expected: PASS, 0 type errors. Fix any remaining stray reference to `clientId`, `hasReview`, `'Pending'`, `'Rejected'`, `Accept(`/`Reject(`, or `/client/` routes that earlier tasks' file lists missed.

- [ ] **Step 15: Final full-repo check**

Run:
```bash
cd backend && dotnet build ImperadorBarberShop.sln && dotnet test tests/ImperadorBarberShop.UnitTests
cd ../frontend && npm run build
```
Expected: both succeed with 0 errors. This is the plan's final gate before integration/E2E (which need Docker/a running dev server respectively, per Tasks 12 and this task's e2e specs).

- [ ] **Step 16: Commit**

```bash
git add -A
git commit -m "feat(frontend): remove client accounts from the rest of the app, finish barber dashboard"
```

---

## Task 18: Documentation — update CLAUDE.md files to match the new contract

**Files:**
- Modify: `CLAUDE.md` (root)
- Modify: `backend/CLAUDE.md`
- Modify: `frontend/CLAUDE.md`

**Interfaces:** none — documentation only. Required by the user's global instruction to keep `CLAUDE.md` current whenever a feature changes the domain/API.

- [ ] **Step 1: Root `CLAUDE.md`** — update the `Entities`, `Enums`, `Business Rules`, and `API Contract` sections

Replace the `Entities` table's `Appointment` row and the `User` row's note, and the `Enums` block:

```markdown
| `User` | Id (Guid), Name, Email, PasswordHash, Role (Barber), CreatedAt — clients are not `User`s; they identify themselves per-booking via name+phone |
| `Barber` | Id (Guid), UserId → User, Availability[], AverageRating (decimal) |
| `BarberAvailability` | Id, BarberId, DayOfWeek (0=Sun…6=Sat), StartTime (TimeOnly), EndTime (TimeOnly) |
| `Service` | Id, Name, Description, DurationMinutes (int), Price (decimal), IsActive |
| `Appointment` | Id, ClientName, ClientPhone, AccessToken (unique, opaque — powers the public manage/cancel/review link), BarberId → Barber, ScheduledAt (DateTime), TotalDurationMinutes, Status, Notes? |
| `AppointmentService` | AppointmentId, ServiceId (join table, M:N) |
| `Review` | Id, AppointmentId, BarberId, Rating (1–5 int), Comment (string?), CreatedAt |
| `RefreshToken` | Id, UserId, TokenHash (BCrypt hashed), ExpiresAt, IsRevoked |
```

```markdown
### Enums

```csharp
public enum UserRole          { Barber = 1 }
public enum AppointmentStatus { Accepted = 0, Cancelled = 1, Completed = 2 }
```
```

Replace the `Business Rules` bullets that reference the removed `Pending`/accept-reject flow and client accounts:

```markdown
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
```

Replace the `Auth (public)`, `Barbers`, `Appointments`, and `Reviews` tables in the `API Contract` section:

```markdown
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
```

Update the `Email Notifications` table to drop the rows for events that no longer send client email (clients have no email):

```markdown
## Email Notifications (via IEmailService)

| Event | Recipients | Subject |
|-------|-----------|---------|
| Appointment created | Barber | "Novo agendamento de {clientName}" |
```

- [ ] **Step 2: `backend/CLAUDE.md`** — update the `Authorization Policies` table

```markdown
## Authorization Policies

| Policy | Required JWT claim |
|--------|--------------------|
| `RequireBarberRole` | `role == "Barber"` |
```

- [ ] **Step 3: `frontend/CLAUDE.md`** — update `Route Structure` and `Auth Strategy`

```markdown
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
```

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md backend/CLAUDE.md frontend/CLAUDE.md
git commit -m "docs: update CLAUDE.md files for anonymous booking + token-based management"
```
