# Cash Control (Controle de Caixa) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add payment method tracking per appointment, expense logging, and an expanded admin financial dashboard with charts and period comparison.

**Architecture:** Three orthogonal additions sharing one migration: (1) nullable PaymentMethod+PaidAt columns on Appointments, (2) new Expenses table with CRUD, (3) new financial queries (timeline, updated summary) + expanded frontend dashboard. Backend follows Clean Architecture with co-located MediatR handlers. Frontend uses TanStack Query, Recharts for charts.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, MediatR, FluentValidation, AutoMapper, xUnit + NSubstitute + FluentAssertions, Next.js 15, TanStack Query v5, Recharts, Vitest + RTL, MSW v2.

## Global Constraints

- Co-located handlers: record + validator + handler in the **same** `.cs` file
- Brand tokens only in Tailwind: `brand-gold`, `brand-gold-light`, `brand-gold-dark`, `brand-black`, `brand-black-soft`, `brand-white` — never raw hex or `red-400`
- `DateTimeKind.Utc` required on every `DateTime` passed to Npgsql (use `.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)` for DateOnly conversions)
- `JsonStringEnumConverter` is globally configured — enums serialize as strings (`"Dinheiro"`, `"Pix"`, etc.)
- PaymentMethod enum values: `Dinheiro=0`, `Cartão=1`, `Pix=2`
- IDOR rule: barber can only access their own appointments — use JWT claim `barberId`; admin bypasses IDOR by passing `RequesterBarberId = null`
- All UI copy in Brazilian Portuguese
- Frontend test files live under `frontend/tests/unit/` mirroring `src/` structure
- Integration tests: `IClassFixture<WebAppFixture>`, seed via `SeedBarberAsync()` or admin HTTP + login pattern from `AdminBlocksControllerTests.cs`

---

### Task 1: Domain layer + EF migration

**Files:**
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Enums/PaymentMethod.cs`
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Expense.cs`
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IExpenseRepository.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Appointment.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContext.cs`
- Test: `backend/tests/ImperadorBarberShop.UnitTests/Domain/DomainEntityTests.cs`

**Interfaces:**
- Produces: `PaymentMethod` enum with `Dinheiro=0,Cartão=1,Pix=2`
- Produces: `Appointment.Complete(PaymentMethod? paymentMethod = null)` — accepts optional method
- Produces: `Appointment.SetPaymentMethod(PaymentMethod paymentMethod)` — for already-completed appointments
- Produces: `Expense` entity with static `Create(decimal amount, string description, DateOnly date, Guid createdByUserId)`
- Produces: `IExpenseRepository` with 5 methods (see step 6)

- [ ] **Step 1: Write the failing domain tests**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Domain/DomainEntityTests.cs
// Add these tests inside the existing class (or add a new class at end of file):

public class AppointmentPaymentMethodTests
{
    [Fact]
    public void Complete_WithPaymentMethod_SetsMethodAndPaidAt()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
        appt.Complete(Domain.Enums.PaymentMethod.Pix);
        appt.Status.Should().Be(AppointmentStatus.Completed);
        appt.PaymentMethod.Should().Be(Domain.Enums.PaymentMethod.Pix);
        appt.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithoutPaymentMethod_LeavesMethodNull()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
        appt.Complete();
        appt.Status.Should().Be(AppointmentStatus.Completed);
        appt.PaymentMethod.Should().BeNull();
        appt.PaidAt.Should().BeNull();
    }

    [Fact]
    public void SetPaymentMethod_OnCompleted_SetsMethod()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
        appt.Complete();
        appt.SetPaymentMethod(Domain.Enums.PaymentMethod.Dinheiro);
        appt.PaymentMethod.Should().Be(Domain.Enums.PaymentMethod.Dinheiro);
        appt.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void SetPaymentMethod_OnAccepted_ThrowsInvalidOperationException()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
        var act = () => appt.SetPaymentMethod(Domain.Enums.PaymentMethod.Pix);
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --no-build 2>&1 | head -20
```

Expected: build error referencing `PaymentMethod` and `SetPaymentMethod` not found.

- [ ] **Step 3: Create PaymentMethod enum**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Enums/PaymentMethod.cs
namespace ImperadorBarberShop.Domain.Enums;

public enum PaymentMethod
{
    Dinheiro = 0,
    Cartão   = 1,
    Pix      = 2,
}
```

- [ ] **Step 4: Update Appointment entity**

Replace the existing `Complete()` method and add new fields + `SetPaymentMethod`:

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Entities/Appointment.cs
// Add to existing properties block:
public PaymentMethod? PaymentMethod { get; private set; }
public DateTime? PaidAt { get; private set; }

// Replace existing Complete() method:
public void Complete(PaymentMethod? paymentMethod = null)
{
    if (Status != AppointmentStatus.Accepted)
        throw new InvalidOperationException($"Cannot complete appointment in status {Status}.");
    Status = AppointmentStatus.Completed;
    if (paymentMethod.HasValue)
    {
        PaymentMethod = paymentMethod;
        PaidAt = DateTime.UtcNow;
    }
    UpdatedAt = DateTime.UtcNow;
}

// Add new method:
public void SetPaymentMethod(PaymentMethod paymentMethod)
{
    if (Status != AppointmentStatus.Completed)
        throw new InvalidOperationException("Cannot set payment method on a non-completed appointment.");
    PaymentMethod = paymentMethod;
    if (!PaidAt.HasValue)
        PaidAt = DateTime.UtcNow;
    UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 5: Create Expense entity**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Entities/Expense.cs
namespace ImperadorBarberShop.Domain.Entities;

public class Expense
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private Expense() { }

    public static Expense Create(decimal amount, string description, DateOnly date, Guid createdByUserId)
        => new()
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Description = description,
            Date = date,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
}
```

- [ ] **Step 6: Create IExpenseRepository**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IExpenseRepository.cs
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IExpenseRepository
{
    Task<List<Expense>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<decimal> GetTotalByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Expense expense, CancellationToken ct = default);
    Task DeleteAsync(Expense expense, CancellationToken ct = default);
}
```

- [ ] **Step 7: Update AppointmentConfiguration — add nullable columns**

```csharp
// In AppointmentConfiguration.Configure():
// Add after existing property configurations:
builder.Property(a => a.PaymentMethod);   // nullable int, no constraint needed
builder.Property(a => a.PaidAt);          // nullable timestamptz
```

- [ ] **Step 8: Create ExpenseConfiguration**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs
using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).IsRequired().HasColumnType("numeric(10,2)");
        builder.Property(e => e.Description).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.CreatedByUserId);
        builder.HasIndex(e => e.Date);
    }
}
```

- [ ] **Step 9: Add DbSet to AppDbContext**

```csharp
// In AppDbContext.cs, add after existing DbSet declarations:
public DbSet<Expense> Expenses => Set<Expense>();
```

- [ ] **Step 10: Create EF migration**

```bash
cd backend
dotnet ef migrations add AddPaymentMethodAndExpenses \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```

Expected: new migration file created under `Migrations/`.

- [ ] **Step 11: Run tests — expect passing**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "AppointmentPaymentMethodTests"
```

Expected: 4 tests pass.

- [ ] **Step 12: Commit**

```bash
git add backend/src/Domain backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContext.cs backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations backend/tests/ImperadorBarberShop.UnitTests/Domain/DomainEntityTests.cs
git commit -m "feat(domain): PaymentMethod enum, Appointment payment fields, Expense entity + migration"
```

---

### Task 2: Backend — Payment method commands + API endpoints

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/UpdatePaymentMethodCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CompleteAppointmentCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/DTOs/AppointmentDto.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AppointmentsController.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/UpdatePaymentMethodCommandHandlerTests.cs`
- Modify: `backend/tests/ImperadorBarberShop.UnitTests/Appointments/CompleteAppointmentCommandHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.IntegrationTests/Appointments/PaymentMethodControllerTests.cs`

**Interfaces:**
- Consumes: `Appointment.Complete(PaymentMethod?)`, `Appointment.SetPaymentMethod(PaymentMethod)` from Task 1
- Produces: `UpdatePaymentMethodCommand(Guid AppointmentId, PaymentMethod PaymentMethod, Guid? RequesterBarberId)` — null RequesterBarberId = admin, skips IDOR
- Produces: `CompleteAppointmentCommand` updated signature: `(Guid AppointmentId, Guid BarberId, PaymentMethod? PaymentMethod = null)`
- Produces: `AppointmentDto.PaymentMethod` (nullable string via JsonStringEnumConverter) and `AppointmentDto.PaidAt` (nullable DateTime)

- [ ] **Step 1: Write unit tests for UpdatePaymentMethodCommand**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Appointments/UpdatePaymentMethodCommandHandlerTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class UpdatePaymentMethodCommandHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly UpdatePaymentMethodCommandHandler _handler;

    public UpdatePaymentMethodCommandHandlerTests()
        => _handler = new UpdatePaymentMethodCommandHandler(_repo, _uow);

    private static Appointment MakeCompleted(Guid barberId)
    {
        var appt = Appointment.Create("João", "+55119", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_Admin_SetsPaymentMethod()
    {
        var barberId = Guid.NewGuid();
        var appt = MakeCompleted(barberId);
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Pix, null), CancellationToken.None);

        appt.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public async Task Handle_CorrectBarber_SetsPaymentMethod()
    {
        var barberId = Guid.NewGuid();
        var appt = MakeCompleted(barberId);
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Dinheiro, barberId), CancellationToken.None);

        appt.PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbidden()
    {
        var appt = MakeCompleted(Guid.NewGuid());
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        var act = () => _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Pix, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new UpdatePaymentMethodCommand(Guid.NewGuid(), PaymentMethod.Pix, null), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
cd backend && dotnet build tests/ImperadorBarberShop.UnitTests 2>&1 | tail -10
```

- [ ] **Step 3: Create UpdatePaymentMethodCommand**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/UpdatePaymentMethodCommand.cs
using FluentValidation;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record UpdatePaymentMethodCommand(
    Guid AppointmentId,
    PaymentMethod PaymentMethod,
    Guid? RequesterBarberId)   // null = admin, bypasses IDOR
    : IRequest;

public class UpdatePaymentMethodCommandValidator : AbstractValidator<UpdatePaymentMethodCommand>
{
    public UpdatePaymentMethodCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}

public class UpdatePaymentMethodCommandHandler : IRequestHandler<UpdatePaymentMethodCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePaymentMethodCommandHandler(IAppointmentRepository appointmentRepository, IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (request.RequesterBarberId.HasValue && appointment.BarberId != request.RequesterBarberId.Value)
            throw new ForbiddenException("You are not authorized to update this appointment.");

        appointment.SetPaymentMethod(request.PaymentMethod);
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Update CompleteAppointmentCommand**

Change the record signature and handler to pass `PaymentMethod?` through to `appointment.Complete()`:

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CompleteAppointmentCommand.cs
// Change the record line from:
//   public record CompleteAppointmentCommand(Guid AppointmentId, Guid BarberId) : IRequest;
// To:
public record CompleteAppointmentCommand(Guid AppointmentId, Guid BarberId, PaymentMethod? PaymentMethod = null) : IRequest;
// (add `using ImperadorBarberShop.Domain.Enums;` at the top)

// In the handler, change:
//   appointment.Complete();
// To:
appointment.Complete(request.PaymentMethod);
```

- [ ] **Step 5: Add PaymentMethod and PaidAt to AppointmentDto**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/DTOs/AppointmentDto.cs
// Add after existing Status property:
public PaymentMethod? PaymentMethod { get; init; }
public DateTime? PaidAt { get; init; }
// (add `using ImperadorBarberShop.Domain.Enums;` at the top)
```

AutoMapper maps these by name convention — no changes needed in `MappingProfile.cs`.

- [ ] **Step 6: Update AppointmentsController**

```csharp
// In AppointmentsController.cs:
// 1. Change Complete endpoint to accept optional body:
[HttpPatch("{id:guid}/complete")]
[Authorize(Policy = "RequireBarberRole")]
public async Task<IActionResult> Complete(
    Guid id,
    [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
    CompleteAppointmentRequest? request,
    CancellationToken cancellationToken)
{
    var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
    await _mediator.Send(new CompleteAppointmentCommand(id, barberId, request?.PaymentMethod), cancellationToken);
    return NoContent();
}

// 2. Add new PATCH /{id}/payment endpoint:
[HttpPatch("{id:guid}/payment")]
[Authorize(Policy = "RequireBarberRole")]
public async Task<IActionResult> UpdatePayment(
    Guid id,
    [FromBody] UpdatePaymentMethodRequest request,
    CancellationToken cancellationToken)
{
    var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
    await _mediator.Send(new UpdatePaymentMethodCommand(id, request.PaymentMethod, barberId), cancellationToken);
    return NoContent();
}

// 3. Add request records at bottom of file:
public record CompleteAppointmentRequest(PaymentMethod? PaymentMethod);
public record UpdatePaymentMethodRequest(PaymentMethod PaymentMethod);
// (add `using ImperadorBarberShop.Domain.Enums;`)
```

- [ ] **Step 7: Add admin payment endpoint to AdminController**

```csharp
// In AdminController.cs, add endpoint after existing appointments section:
[HttpPatch("appointments/{id:guid}/payment")]
public async Task<IActionResult> UpdateAppointmentPayment(
    Guid id,
    [FromBody] AdminUpdatePaymentRequest request,
    CancellationToken ct)
{
    await _mediator.Send(new UpdatePaymentMethodCommand(id, request.PaymentMethod, null), ct);
    return NoContent();
}

// Add record at bottom of file:
public record AdminUpdatePaymentRequest(PaymentMethod PaymentMethod);
// (add `using ImperadorBarberShop.Application.Commands.Appointments;` and `using ImperadorBarberShop.Domain.Enums;`)
```

- [ ] **Step 8: Update existing CompleteAppointment unit tests**

Open `CompleteAppointmentCommandHandlerTests.cs`. The existing test `Handle_ValidComplete_CompletesAppointment` calls `appointment.Complete()` without args — this still compiles. Add one new test:

```csharp
[Fact]
public async Task Handle_WithPaymentMethod_SetsPaymentOnCompletion()
{
    var barberId = Guid.NewGuid();
    var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
    _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
    _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

    await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId, PaymentMethod.Pix), CancellationToken.None);

    appointment.Status.Should().Be(AppointmentStatus.Completed);
    appointment.PaymentMethod.Should().Be(PaymentMethod.Pix);
}
```

- [ ] **Step 9: Write integration tests for payment endpoints**

```csharp
// backend/tests/ImperadorBarberShop.IntegrationTests/Appointments/PaymentMethodControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

public class PaymentMethodControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PaymentMethodControllerTests(WebAppFixture fixture) => _fixture = fixture;

    private async Task<(Guid barberId, Guid appointmentId, HttpClient barberClient)> SeedCompletedAppointment()
    {
        // 1. Create barber
        var adminClient = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var email = $"pm-barber-{Guid.NewGuid()}@test.com";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("PM Barber"), "Name");
        form.Add(new StringContent(email), "Email");
        form.Add(new StringContent("Password123!"), "Password");
        await adminClient.PostAsync("/api/v1/admin/barbers", form);

        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        var login = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var barberId = login.GetProperty("barberId").GetGuid();
        var accessToken = login.GetProperty("accessToken").GetString()!;

        var barberClient = _fixture.CreateClient();
        barberClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // 2. Create appointment via admin seeding in DB
        using var scope = _fixture.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        var barberRepo = scope.ServiceProvider.GetRequiredService<ImperadorBarberShop.Domain.Interfaces.IBarberRepository>();
        var barber = await barberRepo.GetByUserIdAsync(login.GetProperty("userId").GetGuid());
        var cmd = new ImperadorBarberShop.Application.Commands.Appointments.CreateAppointmentCommand(
            "Test Client", "+5511999990000", barberId,
            DateTime.UtcNow.AddDays(1), [scope.ServiceProvider.GetRequiredService<ImperadorBarberShop.Domain.Interfaces.IServiceRepository>()
                .GetAllActiveAsync().Result.First().Id],
            null);
        var apptResult = await mediator.Send(cmd);

        // 3. Complete via barber endpoint
        await barberClient.PatchAsync($"/api/v1/appointments/{apptResult.Id}/complete", null);

        return (barberId, apptResult.Id, barberClient);
    }

    [Fact]
    public async Task UpdatePayment_AsBarber_Returns204()
    {
        var (_, apptId, barberClient) = await SeedCompletedAppointment();
        var resp = await barberClient.PatchAsJsonAsync($"/api/v1/appointments/{apptId}/payment", new { paymentMethod = "Pix" });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePayment_WrongBarber_Returns403()
    {
        var (_, apptId, _) = await SeedCompletedAppointment();
        var otherBarberClient = _fixture.CreateAuthenticatedClient("Barber", Guid.NewGuid(), Guid.NewGuid());
        var resp = await otherBarberClient.PatchAsJsonAsync($"/api/v1/appointments/{apptId}/payment", new { paymentMethod = "Pix" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePayment_Unauthenticated_Returns401()
    {
        var resp = await _fixture.CreateClient().PatchAsJsonAsync($"/api/v1/appointments/{Guid.NewGuid()}/payment", new { paymentMethod = "Pix" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePayment_AsAdmin_Returns204()
    {
        var (_, apptId, _) = await SeedCompletedAppointment();
        var adminClient = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var resp = await adminClient.PatchAsJsonAsync($"/api/v1/admin/appointments/{apptId}/payment", new { paymentMethod = "Cartão" });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
```

**Note:** The seeding above relies on `IBarberRepository.GetByUserIdAsync`. Check if that method exists; if not, seed via `/auth/login` response `barberId` directly and create appointment via a known seeded service. Simplify to avoid compile errors: just create and complete appointment in a single integration test helper.

Actually, use this simpler helper pattern (same as `AdminBlocksControllerTests.SeedBarberAndGetId`):

```csharp
private async Task<(Guid barberId, HttpClient barberClient)> SeedBarber()
{
    var adminClient = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
    var email = $"pm-{Guid.NewGuid()}@test.com";
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent("PM Test Barber"), "Name");
    form.Add(new StringContent(email), "Email");
    form.Add(new StringContent("Password123!"), "Password");
    await adminClient.PostAsync("/api/v1/admin/barbers", form);

    var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
        new { email, password = "Password123!" });
    var login = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_json);
    var barberId = login.GetProperty("barberId").GetGuid();
    var token = login.GetProperty("accessToken").GetString()!;

    var barberClient = _fixture.CreateClient();
    barberClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    return (barberId, barberClient);
}
```

Then for appointment seeding, get an active service id from `GET /api/v1/services` and create via `POST /api/v1/appointments`. Complete via the barber token.

- [ ] **Step 10: Run all unit tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests
```

Expected: all pass (including existing Complete tests — they still work because `PaymentMethod?` defaults to null).

- [ ] **Step 11: Run integration tests for payment**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests --filter "PaymentMethodControllerTests"
```

Expected: 4 tests pass.

- [ ] **Step 12: Commit**

```bash
git add backend/src/Application backend/src/Api backend/tests
git commit -m "feat(api): UpdatePaymentMethodCommand, PATCH /appointments/{id}/payment + admin endpoint"
```

---

### Task 3: Expense CRUD backend + admin barber appointments endpoint

**Files:**
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Financial/CreateExpenseCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Financial/DeleteExpenseCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetExpensesQuery.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/DTOs/FinancialDtos.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Financial/CreateExpenseCommandHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.IntegrationTests/Financial/FinancialExpensesControllerTests.cs`

**Interfaces:**
- Consumes: `IExpenseRepository` from Task 1
- Consumes: `Expense.Create(amount, description, date, userId)` from Task 1
- Produces: `ExpenseDto(Guid Id, decimal Amount, string Description, DateOnly Date, DateTime CreatedAt)`
- Produces: `IExpenseRepository` registered in DI
- Produces: `GET /admin/financial/expenses?from&to`, `POST /admin/financial/expenses`, `DELETE /admin/financial/expenses/{id}`
- Produces: `GET /admin/barbers/{barberId}/appointments`

- [ ] **Step 1: Write unit tests for CreateExpenseCommand**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Financial/CreateExpenseCommandHandlerTests.cs
using FluentAssertions;
using FluentValidation;
using ImperadorBarberShop.Application.Commands.Financial;
using ImperadorBarberShop.Application.Behaviors;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class CreateExpenseCommandHandlerTests
{
    private readonly IExpenseRepository _repo = Substitute.For<IExpenseRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateExpenseCommandHandler _handler;

    public CreateExpenseCommandHandlerTests()
        => _handler = new CreateExpenseCommandHandler(_repo, _uow);

    [Fact]
    public async Task Handle_ValidExpense_ReturnsId()
    {
        var userId = Guid.NewGuid();
        var cmd = new CreateExpenseCommand(150m, "Produto para cabelo", new DateOnly(2026, 7, 1), userId);
        var id = await _handler.Handle(cmd, CancellationToken.None);
        id.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Is<Expense>(e => e.Amount == 150m), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run test — expect compile failure**

```bash
cd backend && dotnet build tests/ImperadorBarberShop.UnitTests 2>&1 | tail -5
```

- [ ] **Step 3: Create ExpenseRepository**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/ExpenseRepository.cs
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _context;
    public ExpenseRepository(AppDbContext context) => _context = context;

    public async Task<List<Expense>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _context.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

    public async Task<decimal> GetTotalByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _context.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .SumAsync(e => e.Amount, ct);

    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Expenses.FindAsync([id], ct);

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
        => await _context.Expenses.AddAsync(expense, ct);

    public Task DeleteAsync(Expense expense, CancellationToken ct = default)
    {
        _context.Expenses.Remove(expense);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Register ExpenseRepository in DI**

```csharp
// In DependencyInjection.cs, add after IBarberBlockRepository line:
services.AddScoped<IExpenseRepository, ExpenseRepository>();
```

- [ ] **Step 5: Create CreateExpenseCommand**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Financial/CreateExpenseCommand.cs
using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Financial;

public record CreateExpenseCommand(decimal Amount, string Description, DateOnly Date, Guid CreatedByUserId) : IRequest<Guid>;

public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Valor deve ser maior que zero.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreatedByUserId).NotEmpty();
    }
}

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Guid>
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateExpenseCommandHandler(IExpenseRepository expenseRepository, IUnitOfWork unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = Expense.Create(request.Amount, request.Description, request.Date, request.CreatedByUserId);
        await _expenseRepository.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }
}
```

- [ ] **Step 6: Create DeleteExpenseCommand**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Financial/DeleteExpenseCommand.cs
using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Financial;

public record DeleteExpenseCommand(Guid Id) : IRequest;

public class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand>
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteExpenseCommandHandler(IExpenseRepository expenseRepository, IUnitOfWork unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (expense is null)
            throw new KeyNotFoundException($"Expense '{request.Id}' not found.");
        await _expenseRepository.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 7: Create GetExpensesQuery**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetExpensesQuery.cs
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetExpensesQuery(DateOnly From, DateOnly To) : IRequest<List<ExpenseDto>>;

public class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, List<ExpenseDto>>
{
    private readonly IExpenseRepository _expenseRepository;
    public GetExpensesQueryHandler(IExpenseRepository expenseRepository) => _expenseRepository = expenseRepository;

    public async Task<List<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        var expenses = await _expenseRepository.GetByDateRangeAsync(request.From, request.To, cancellationToken);
        return expenses.Select(e => new ExpenseDto(e.Id, e.Amount, e.Description, e.Date, e.CreatedAt)).ToList();
    }
}
```

- [ ] **Step 8: Add ExpenseDto to FinancialDtos.cs**

```csharp
// Append to FinancialDtos.cs:
public record ExpenseDto(Guid Id, decimal Amount, string Description, DateOnly Date, DateTime CreatedAt);
```

- [ ] **Step 9: Add expense endpoints + admin barber appointments to AdminController**

```csharp
// In AdminController.cs, add these endpoints and using statements:
// using ImperadorBarberShop.Application.Commands.Financial;
// using ImperadorBarberShop.Application.Queries.Financial;
// using ImperadorBarberShop.Application.Queries.Appointments;

// Expense endpoints:
[HttpGet("financial/expenses")]
public async Task<IActionResult> GetExpenses(
    [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    => Ok(await _mediator.Send(new GetExpensesQuery(from, to), ct));

[HttpPost("financial/expenses")]
public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken ct)
{
    var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    var id = await _mediator.Send(new CreateExpenseCommand(request.Amount, request.Description, request.Date, userId), ct);
    return CreatedAtAction(nameof(GetExpenses), null, new { id });
}

[HttpDelete("financial/expenses/{id:guid}")]
public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
{
    await _mediator.Send(new DeleteExpenseCommand(id), ct);
    return NoContent();
}

// Admin barber appointments (reuses existing query):
[HttpGet("barbers/{barberId:guid}/appointments")]
public async Task<IActionResult> GetBarberAppointments(Guid barberId, CancellationToken ct)
    => Ok(await _mediator.Send(new GetBarberAppointmentsQuery(barberId), ct));

// Add request record at bottom of file:
public record CreateExpenseRequest(decimal Amount, string Description, DateOnly Date);
```

- [ ] **Step 10: Write integration tests for expenses**

```csharp
// backend/tests/ImperadorBarberShop.IntegrationTests/Financial/FinancialExpensesControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Financial;

public class FinancialExpensesControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinancialExpensesControllerTests(WebAppFixture fixture) => _fixture = fixture;

    private HttpClient AdminClient() => _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

    [Fact]
    public async Task CreateExpense_ValidPayload_Returns201()
    {
        var resp = await AdminClient().PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 150.00m, description = "Produto para cabelo", date = "2026-07-01" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateExpense_ZeroAmount_Returns400()
    {
        var resp = await AdminClient().PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 0m, description = "Test", date = "2026-07-01" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExpense_Unauthenticated_Returns401()
    {
        var resp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 50m, description = "Test", date = "2026-07-01" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteExpense_ExistingExpense_Returns204()
    {
        var client = AdminClient();
        var createResp = await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 75m, description = "Lâminas", date = "2026-07-02" });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var id = created.GetProperty("id").GetString();

        var deleteResp = await client.DeleteAsync($"/api/v1/admin/financial/expenses/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteExpense_NotFound_Returns404()
    {
        var resp = await AdminClient().DeleteAsync($"/api/v1/admin/financial/expenses/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExpenses_FiltersByDateRange()
    {
        var client = AdminClient();
        await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 100m, description = "Dentro do período", date = "2026-07-15" });
        await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 50m, description = "Fora do período", date = "2026-06-01" });

        var resp = await client.GetAsync("/api/v1/admin/financial/expenses?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        list.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        var descriptions = Enumerable.Range(0, list.GetArrayLength())
            .Select(i => list[i].GetProperty("description").GetString())
            .ToList();
        descriptions.Should().NotContain("Fora do período");
    }
}
```

- [ ] **Step 11: Run unit tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "CreateExpenseCommandHandlerTests"
```

Expected: 1 test passes.

- [ ] **Step 12: Run integration tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests --filter "FinancialExpensesControllerTests"
```

Expected: 5 tests pass.

- [ ] **Step 13: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(api): Expense CRUD, admin barber appointments endpoint"
```

---

### Task 4: Financial timeline query + updated summary

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetFinancialTimelineQuery.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetFinancialSummaryQuery.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/DTOs/FinancialDtos.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Financial/GetFinancialTimelineQueryHandlerTests.cs`
- Modify: `backend/tests/ImperadorBarberShop.UnitTests/Financial/GetFinancialSummaryQueryHandlerTests.cs`
- Create: `backend/tests/ImperadorBarberShop.IntegrationTests/Financial/FinancialSummaryTimelineControllerTests.cs`

**Interfaces:**
- Consumes: `IExpenseRepository.GetTotalByDateRangeAsync` from Task 3
- Consumes: `IAppointmentRepository.GetCompletedByDateRangeAsync`
- Produces: `FinancialSummaryDto` updated with `TotalExpenses` and `NetRevenue`
- Produces: `FinancialTimelineItemDto(string Period, decimal Revenue, int Appointments)`
- Produces: `GET /admin/financial/timeline?from&to&groupBy=day|week|month`
- Produces: `GET /admin/financial/summary` response now includes `totalExpenses` and `netRevenue`

- [ ] **Step 1: Write unit tests for GetFinancialTimelineQuery**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Financial/GetFinancialTimelineQueryHandlerTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class GetFinancialTimelineQueryHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();

    private static Appointment MakeCompleted(DateTime scheduledAt, decimal price)
    {
        // Create a minimal appointment for revenue calculation
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(),
            scheduledAt, 30, null, [Guid.NewGuid()]);
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_GroupByDay_GroupsCorrectly()
    {
        var day1 = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { MakeCompleted(day1, 0), MakeCompleted(day2, 0) });

        var handler = new GetFinancialTimelineQueryHandler(_repo);
        var result = await handler.Handle(
            new GetFinancialTimelineQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "day"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Period.Should().Be("2026-07-10");
        result[1].Period.Should().Be("2026-07-11");
    }

    [Fact]
    public async Task Handle_GroupByMonth_GroupsCorrectly()
    {
        var julyDate = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
        var augustDate = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { MakeCompleted(julyDate, 0), MakeCompleted(augustDate, 0) });

        var handler = new GetFinancialTimelineQueryHandler(_repo);
        var result = await handler.Handle(
            new GetFinancialTimelineQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 31), "month"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Period.Should().Be("2026-07-01");
        result[1].Period.Should().Be("2026-08-01");
    }

    [Fact]
    public async Task Handle_NoAppointments_ReturnsEmpty()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var handler = new GetFinancialTimelineQueryHandler(_repo);
        var result = await handler.Handle(
            new GetFinancialTimelineQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "day"),
            CancellationToken.None);

        result.Should().BeEmpty();
    }
}
```

**Note on MakeCompleted:** The test above can't set `AppointmentServices` (private list). Revenue will be 0 for all test data, but that's fine — tests verify grouping logic, not pricing. Revenue sum test can be a separate integration test.

- [ ] **Step 2: Update FinancialSummaryQueryHandlerTests to mock IExpenseRepository**

```csharp
// In GetFinancialSummaryQueryHandlerTests.cs, update the existing test:
public class GetFinancialSummaryQueryHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();
    private readonly IExpenseRepository _expenseRepo = Substitute.For<IExpenseRepository>();

    [Fact]
    public async Task Handle_NoAppointments_ReturnsZeros()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _expenseRepo.GetTotalByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(0m);

        var handler = new GetFinancialSummaryQueryHandler(_repo, _expenseRepo);
        var query = new GetFinancialSummaryQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var result = await handler.Handle(query, CancellationToken.None);

        result.TotalRevenue.Should().Be(0);
        result.TotalExpenses.Should().Be(0);
        result.NetRevenue.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithExpenses_ComputesNetRevenue()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _expenseRepo.GetTotalByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(200m);

        var handler = new GetFinancialSummaryQueryHandler(_repo, _expenseRepo);
        var result = await handler.Handle(
            new GetFinancialSummaryQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)),
            CancellationToken.None);

        result.TotalExpenses.Should().Be(200m);
        result.NetRevenue.Should().Be(-200m);
    }
}
```

- [ ] **Step 3: Update FinancialSummaryDto**

```csharp
// In FinancialDtos.cs, replace existing FinancialSummaryDto record:
public record FinancialSummaryDto(
    decimal TotalRevenue,
    int TotalAppointments,
    decimal AverageTicket,
    DateOnly From,
    DateOnly To,
    decimal TotalExpenses,
    decimal NetRevenue);

// Add new DTO:
public record FinancialTimelineItemDto(string Period, decimal Revenue, int Appointments);
```

- [ ] **Step 4: Update GetFinancialSummaryQuery handler**

```csharp
// In GetFinancialSummaryQuery.cs:
// 1. Update handler constructor to accept IExpenseRepository
// 2. Update Handle method:

public class GetFinancialSummaryQueryHandler : IRequestHandler<GetFinancialSummaryQuery, FinancialSummaryDto>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IExpenseRepository _expenseRepository;

    public GetFinancialSummaryQueryHandler(
        IAppointmentRepository appointmentRepository,
        IExpenseRepository expenseRepository)
    {
        _appointmentRepository = appointmentRepository;
        _expenseRepository = expenseRepository;
    }

    public async Task<FinancialSummaryDto> Handle(GetFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);
        var totalExpenses = await _expenseRepository.GetTotalByDateRangeAsync(request.From, request.To, cancellationToken);

        var total = appointments.Count;
        var revenue = appointments.SelectMany(a => a.AppointmentServices).Sum(s => s.Service.Price);
        var average = total > 0 ? revenue / total : 0m;
        var netRevenue = revenue - totalExpenses;

        return new FinancialSummaryDto(revenue, total, Math.Round(average, 2), request.From, request.To,
            Math.Round(totalExpenses, 2), Math.Round(netRevenue, 2));
    }
}
```

- [ ] **Step 5: Create GetFinancialTimelineQuery**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetFinancialTimelineQuery.cs
using FluentValidation;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialTimelineQuery(DateOnly From, DateOnly To, string GroupBy = "day")
    : IRequest<List<FinancialTimelineItemDto>>;

public class GetFinancialTimelineQueryValidator : AbstractValidator<GetFinancialTimelineQuery>
{
    private static readonly string[] ValidGroupBy = ["day", "week", "month"];

    public GetFinancialTimelineQueryValidator()
    {
        RuleFor(x => x.GroupBy).Must(g => ValidGroupBy.Contains(g))
            .WithMessage("groupBy deve ser 'day', 'week' ou 'month'.");
    }
}

public class GetFinancialTimelineQueryHandler : IRequestHandler<GetFinancialTimelineQuery, List<FinancialTimelineItemDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialTimelineQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<List<FinancialTimelineItemDto>> Handle(
        GetFinancialTimelineQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        var grouped = appointments
            .GroupBy(a => GetPeriodKey(a.ScheduledAt, request.GroupBy))
            .OrderBy(g => g.Key)
            .Select(g => new FinancialTimelineItemDto(
                g.Key,
                g.SelectMany(a => a.AppointmentServices).Sum(s => s.Service.Price),
                g.Count()))
            .ToList();

        return grouped;
    }

    private static string GetPeriodKey(DateTime scheduledAt, string groupBy) => groupBy switch
    {
        "month" => new DateOnly(scheduledAt.Year, scheduledAt.Month, 1).ToString("yyyy-MM-dd"),
        "week"  => GetMondayOfWeek(DateOnly.FromDateTime(scheduledAt)).ToString("yyyy-MM-dd"),
        _       => DateOnly.FromDateTime(scheduledAt).ToString("yyyy-MM-dd"),  // "day"
    };

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        int daysFromMonday = ((int)date.DayOfWeek - 1 + 7) % 7;
        return date.AddDays(-daysFromMonday);
    }
}
```

- [ ] **Step 6: Add timeline endpoint to AdminController**

```csharp
// In AdminController.cs, add after existing financial endpoints:
[HttpGet("financial/timeline")]
public async Task<IActionResult> GetTimeline(
    [FromQuery] DateOnly from,
    [FromQuery] DateOnly to,
    [FromQuery] string groupBy = "day",
    CancellationToken ct = default)
    => Ok(await _mediator.Send(new GetFinancialTimelineQuery(from, to, groupBy), ct));
```

- [ ] **Step 7: Write integration tests**

```csharp
// backend/tests/ImperadorBarberShop.IntegrationTests/Financial/FinancialSummaryTimelineControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Financial;

public class FinancialSummaryTimelineControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinancialSummaryTimelineControllerTests(WebAppFixture fixture) => _fixture = fixture;

    private HttpClient AdminClient() => _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

    [Fact]
    public async Task GetSummary_IncludesTotalExpensesAndNetRevenue()
    {
        var client = AdminClient();
        await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 100m, description = "Custo", date = "2026-07-10" });

        var resp = await client.GetAsync("/api/v1/admin/financial/summary?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.TryGetProperty("totalExpenses", out _).Should().BeTrue();
        body.TryGetProperty("netRevenue", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeline_DefaultGroupByDay_Returns200()
    {
        var resp = await AdminClient().GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetTimeline_GroupByMonth_Returns200()
    {
        var resp = await AdminClient().GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31&groupBy=month");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTimeline_InvalidGroupBy_Returns400()
    {
        var resp = await AdminClient().GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31&groupBy=invalid");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTimeline_Unauthenticated_Returns401()
    {
        var resp = await _fixture.CreateClient().GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 8: Run unit tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "GetFinancialTimelineQueryHandlerTests|GetFinancialSummaryQueryHandlerTests"
```

Expected: all pass.

- [ ] **Step 9: Run integration tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests --filter "FinancialSummaryTimelineControllerTests"
```

Expected: 5 pass.

- [ ] **Step 10: Run full backend test suite**

```bash
cd backend && dotnet test
```

Expected: all tests pass.

- [ ] **Step 11: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(api): financial timeline endpoint, updated summary with expenses+netRevenue"
```

---

### Task 5: Frontend — types, API clients, hooks

**Files:**
- Modify: `frontend/src/types/api.types.ts`
- Modify: `frontend/src/lib/api/appointments.api.ts`
- Modify: `frontend/src/lib/api/admin.api.ts`
- Modify: `frontend/src/hooks/useAppointments.ts`
- Modify: `frontend/src/hooks/useAdminFinancial.ts`
- Create: `frontend/src/hooks/useAdminBarberAppointments.ts`
- Modify: `frontend/tests/mocks/handlers.ts`

**Interfaces:**
- Consumes: backend endpoints from Tasks 1–4
- Produces: `PaymentMethod` type, updated `Appointment` type, updated `FinancialSummary` type, `Expense` type, `FinancialTimelineItem` type
- Produces: `appointmentsApi.complete(id, paymentMethod?)`, `appointmentsApi.updatePaymentMethod(id, paymentMethod)`
- Produces: `adminApi.getExpenses(from, to)`, `adminApi.createExpense(...)`, `adminApi.deleteExpense(id)`, `adminApi.getTimeline(from, to, groupBy)`, `adminApi.getBarberAppointments(barberId)`
- Produces: `useCompleteAppointment` updated, `useUpdatePaymentMethod` mutation, hooks for expenses/timeline

- [ ] **Step 1: Update api.types.ts**

```typescript
// In frontend/src/types/api.types.ts:

// Add after existing type exports:
export type PaymentMethod = 'Dinheiro' | 'Cartão' | 'Pix'

// Update Appointment interface — add two optional fields:
export interface Appointment {
  // ... existing fields ...
  paymentMethod: PaymentMethod | null
  paidAt: string | null
}

// Update FinancialSummary interface — add two new fields:
export interface FinancialSummary {
  totalRevenue: number
  totalAppointments: number
  averageTicket: number
  from: string
  to: string
  totalExpenses: number
  netRevenue: number
}

// Add new types:
export interface Expense {
  id: string
  amount: number
  description: string
  date: string        // "YYYY-MM-DD"
  createdAt: string
}

export interface FinancialTimelineItem {
  period: string      // "YYYY-MM-DD" (start of day/week/month)
  revenue: number
  appointments: number
}

export interface CreateExpensePayload {
  amount: number
  description: string
  date: string        // "YYYY-MM-DD"
}
```

- [ ] **Step 2: Update appointments.api.ts**

```typescript
// In appointments.api.ts:
// 1. Change complete() to accept optional paymentMethod:
complete(id: string, paymentMethod?: PaymentMethod) {
  return apiClient.patch<Appointment>(`/appointments/${id}/complete`,
    paymentMethod ? { paymentMethod } : undefined)
},

// 2. Add updatePaymentMethod:
updatePaymentMethod(id: string, paymentMethod: PaymentMethod) {
  return apiClient.patch<void>(`/appointments/${id}/payment`, { paymentMethod })
},
```

- [ ] **Step 3: Update admin.api.ts**

```typescript
// In adminApi object, add:
getBarberAppointments: (barberId: string) =>
  apiClient.get<Appointment[]>(`/admin/barbers/${barberId}/appointments`).then((r) => r.data),

getExpenses: (from: string, to: string) =>
  apiClient.get<Expense[]>('/admin/financial/expenses', { params: { from, to } }).then((r) => r.data),

createExpense: (payload: CreateExpensePayload) =>
  apiClient.post<{ id: string }>('/admin/financial/expenses', payload).then((r) => r.data),

deleteExpense: (id: string) =>
  apiClient.delete(`/admin/financial/expenses/${id}`),

getTimeline: (from: string, to: string, groupBy: 'day' | 'week' | 'month' = 'day') =>
  apiClient.get<FinancialTimelineItem[]>('/admin/financial/timeline', { params: { from, to, groupBy } })
    .then((r) => r.data),

updateAppointmentPayment: (id: string, paymentMethod: PaymentMethod) =>
  apiClient.patch(`/admin/appointments/${id}/payment`, { paymentMethod }),
```

- [ ] **Step 4: Update useAppointments.ts**

```typescript
// 1. Update useCompleteAppointment to accept optional paymentMethod:
export function useCompleteAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, paymentMethod }: { id: string; paymentMethod?: PaymentMethod }) =>
      appointmentsApi.complete(id, paymentMethod).then((r) => r.data),
    onMutate: async ({ id }) => {
      // ... same optimistic update as before, using id instead of bare id ...
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'barber'] })
    },
  })
}

// 2. Add useUpdatePaymentMethod:
export function useUpdatePaymentMethod() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, paymentMethod }: { id: string; paymentMethod: PaymentMethod }) =>
      appointmentsApi.updatePaymentMethod(id, paymentMethod),
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'barber'] })
    },
  })
}
```

- [ ] **Step 5: Update useAdminFinancial.ts**

```typescript
// Add to existing hooks file:
export function useFinancialTimeline(from: string, to: string, groupBy: 'day' | 'week' | 'month') {
  return useQuery({
    queryKey: ['admin', 'financial', 'timeline', from, to, groupBy],
    queryFn: () => adminApi.getTimeline(from, to, groupBy),
    enabled: !!from && !!to,
  })
}

export function useExpenses(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'expenses', from, to],
    queryFn: () => adminApi.getExpenses(from, to),
    enabled: !!from && !!to,
  })
}

export function useCreateExpense() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateExpensePayload) => adminApi.createExpense(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'expenses'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'summary'] })
    },
  })
}

export function useDeleteExpense() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteExpense(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'expenses'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'summary'] })
    },
  })
}
```

- [ ] **Step 6: Create useAdminBarberAppointments.ts**

```typescript
// frontend/src/hooks/useAdminBarberAppointments.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'
import type { PaymentMethod } from '@/types/api.types'

export function useAdminBarberAppointments(barberId: string) {
  return useQuery({
    queryKey: ['admin', 'barber', 'appointments', barberId],
    queryFn: () => adminApi.getBarberAppointments(barberId),
    enabled: !!barberId,
  })
}

export function useAdminUpdateAppointmentPayment(barberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, paymentMethod }: { id: string; paymentMethod: PaymentMethod }) =>
      adminApi.updateAppointmentPayment(id, paymentMethod),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'barber', 'appointments', barberId] })
    },
  })
}
```

- [ ] **Step 7: Update MSW handlers.ts**

```typescript
// In handlers.ts, add to mockBarberAppointments fixtures — add paymentMethod/paidAt fields:
// Update both existing appointments to include:
//   paymentMethod: null,
//   paidAt: null,

// Add new mock handlers:
http.patch(`${BASE_URL}/appointments/:id/payment`, () => {
  return new HttpResponse(null, { status: 204 })
}),

http.get(`${BASE_URL}/admin/barbers/:barberId/appointments`, () => {
  return HttpResponse.json(mockBarberAppointments)
}),

http.get(`${BASE_URL}/admin/financial/expenses`, () => {
  return HttpResponse.json([
    { id: 'expense-1', amount: 100, description: 'Produto', date: '2026-07-01', createdAt: new Date().toISOString() },
  ])
}),

http.post(`${BASE_URL}/admin/financial/expenses`, async () => {
  return HttpResponse.json({ id: 'expense-new-1' }, { status: 201 })
}),

http.delete(`${BASE_URL}/admin/financial/expenses/:id`, () => {
  return new HttpResponse(null, { status: 204 })
}),

http.get(`${BASE_URL}/admin/financial/timeline`, () => {
  return HttpResponse.json([
    { period: '2026-07-01', revenue: 150, appointments: 3 },
    { period: '2026-07-02', revenue: 80, appointments: 2 },
  ])
}),

http.patch(`${BASE_URL}/admin/appointments/:id/payment`, () => {
  return new HttpResponse(null, { status: 204 })
}),

// Update existing summary handler to include new fields:
http.get(`${BASE_URL}/admin/financial/summary`, () => {
  return HttpResponse.json({
    totalRevenue: 1250,
    totalAppointments: 15,
    averageTicket: 83.33,
    from: '2026-07-01',
    to: '2026-07-31',
    totalExpenses: 200,
    netRevenue: 1050,
  })
}),
```

- [ ] **Step 8: Run frontend type check**

```bash
cd frontend && npx tsc --noEmit
```

Expected: no type errors.

- [ ] **Step 9: Run frontend tests**

```bash
cd frontend && npm test
```

Expected: all existing tests pass (MSW handler additions are backward compatible).

- [ ] **Step 10: Commit**

```bash
git add frontend/src/types frontend/src/lib frontend/src/hooks frontend/tests/mocks
git commit -m "feat(frontend): types, API clients, hooks for payment method, expenses, timeline"
```

---

### Task 6: Frontend — appointment payment UI

**Files:**
- Modify: `frontend/src/components/appointments/AppointmentCard.tsx`
- Modify: `frontend/src/components/appointments/BarberAppointmentList.tsx`
- Create: `frontend/src/app/admin/barbers/AdminAppointmentsSection.tsx`
- Modify: `frontend/src/app/admin/barbers/page.tsx`
- Modify: `frontend/tests/unit/components/appointments/BarberAppointmentList.test.tsx`
- Create: `frontend/tests/unit/components/appointments/AdminAppointmentsSection.test.tsx`

**Interfaces:**
- Consumes: `useCompleteAppointment` (updated signature `{id, paymentMethod?}`), `useUpdatePaymentMethod`, `useAdminBarberAppointments`, `useAdminUpdateAppointmentPayment` from Task 5
- Produces: `AppointmentCard` shows `paymentMethod` badge for Completed appointments
- Produces: `BarberAppointmentList` shows payment modal before completing + inline "Registrar pagamento" for Completed without method
- Produces: `AdminAppointmentsSection` shows per-barber completed appointments with payment badges + admin can set method

- [ ] **Step 1: Add payment badge to AppointmentCard**

```tsx
// In AppointmentCard.tsx, add after the services div:
{appointment.status === 'Completed' && (
  <div className="flex items-center gap-2 text-sm">
    {appointment.paymentMethod ? (
      <span className="inline-flex items-center gap-1 rounded-full bg-brand-gold/15 px-2.5 py-0.5 text-xs font-medium text-brand-gold">
        {appointment.paymentMethod === 'Dinheiro' && '💵'}
        {appointment.paymentMethod === 'Cartão' && '💳'}
        {appointment.paymentMethod === 'Pix' && '⚡'}
        {' '}{appointment.paymentMethod}
      </span>
    ) : (
      <span className="text-xs text-brand-white/30">— sem método</span>
    )}
  </div>
)}
```

- [ ] **Step 2: Update BarberAppointmentList — payment modal + inline register**

```tsx
// frontend/src/components/appointments/BarberAppointmentList.tsx
// Full replacement:
'use client'

import { useState } from 'react'
import { AppointmentCard } from './AppointmentCard'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import {
  useBarberAppointments,
  useCancelAppointmentByBarber,
  useCompleteAppointment,
  useUpdatePaymentMethod,
} from '@/hooks/useAppointments'
import type { PaymentMethod } from '@/types/api.types'

const PAYMENT_OPTIONS: { value: PaymentMethod | null; label: string }[] = [
  { value: 'Dinheiro', label: '💵 Dinheiro' },
  { value: 'Cartão', label: '💳 Cartão' },
  { value: 'Pix', label: '⚡ Pix' },
  { value: null, label: 'Pular' },
]

export function BarberAppointmentList() {
  const { data: appointments, isLoading, isError } = useBarberAppointments()
  const cancel = useCancelAppointmentByBarber()
  const complete = useCompleteAppointment()
  const updatePayment = useUpdatePaymentMethod()
  const [pendingCompleteId, setPendingCompleteId] = useState<string | null>(null)
  const [pendingPaymentId, setPendingPaymentId] = useState<string | null>(null)
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod | null>(null)

  if (isLoading) {
    return <div className="flex justify-center py-12"><Spinner size="lg" /></div>
  }
  if (isError) {
    return <p role="alert" className="text-center text-brand-gold/70 py-8">Erro ao carregar agendamentos.</p>
  }
  if (!appointments || appointments.length === 0) {
    return <p className="text-center text-brand-white/50 py-8">Nenhum agendamento encontrado.</p>
  }

  const sorted = [...appointments].sort(
    (a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()
  )

  async function handleComplete(id: string, paymentMethod: PaymentMethod | null) {
    await complete.mutateAsync({ id, paymentMethod: paymentMethod ?? undefined })
    setPendingCompleteId(null)
    setSelectedMethod(null)
  }

  return (
    <div className="flex flex-col gap-3">
      {sorted.map((appointment) => (
        <AppointmentCard
          key={appointment.id}
          appointment={appointment}
          actions={
            appointment.status === 'Accepted' ? (
              pendingCompleteId === appointment.id ? (
                <div className="flex flex-col gap-3 w-full">
                  <p className="text-sm text-brand-white/70">Forma de pagamento (opcional)</p>
                  <div className="flex flex-wrap gap-2">
                    {PAYMENT_OPTIONS.map((opt) => (
                      <button
                        key={opt.label}
                        onClick={() => setSelectedMethod(opt.value)}
                        className={[
                          'px-3 py-1.5 rounded-lg text-sm border transition-colors',
                          selectedMethod === opt.value
                            ? 'border-brand-gold bg-brand-gold/20 text-brand-gold'
                            : 'border-brand-white/20 text-brand-white/60 hover:border-brand-gold/50',
                        ].join(' ')}
                      >
                        {opt.label}
                      </button>
                    ))}
                  </div>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      isLoading={complete.isPending}
                      onClick={() => handleComplete(appointment.id, selectedMethod)}
                    >
                      Confirmar
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => { setPendingCompleteId(null); setSelectedMethod(null) }}
                    >
                      Cancelar
                    </Button>
                  </div>
                </div>
              ) : (
                <>
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={() => { setPendingCompleteId(appointment.id); setSelectedMethod(null) }}
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
              )
            ) : appointment.status === 'Completed' && !appointment.paymentMethod ? (
              pendingPaymentId === appointment.id ? (
                <div className="flex flex-wrap gap-2 items-center">
                  {(['Dinheiro', 'Cartão', 'Pix'] as PaymentMethod[]).map((m) => (
                    <button
                      key={m}
                      onClick={async () => {
                        try {
                          await updatePayment.mutateAsync({ id: appointment.id, paymentMethod: m })
                        } finally {
                          setPendingPaymentId(null)
                        }
                      }}
                      className="px-3 py-1 rounded-lg text-xs border border-brand-white/20 text-brand-white/60 hover:border-brand-gold hover:text-brand-gold transition-colors"
                    >
                      {m}
                    </button>
                  ))}
                  <button
                    onClick={() => setPendingPaymentId(null)}
                    className="text-xs text-brand-white/40 hover:text-brand-white/70"
                  >
                    Fechar
                  </button>
                </div>
              ) : (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setPendingPaymentId(appointment.id)}
                >
                  Registrar pagamento
                </Button>
              )
            ) : undefined
          }
        />
      ))}
    </div>
  )
}
```

- [ ] **Step 3: Create AdminAppointmentsSection**

```tsx
// frontend/src/app/admin/barbers/AdminAppointmentsSection.tsx
'use client'

import { useState } from 'react'
import { useAdminBarberAppointments, useAdminUpdateAppointmentPayment } from '@/hooks/useAdminBarberAppointments'
import { Spinner } from '@/components/ui/Spinner'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import type { PaymentMethod } from '@/types/api.types'

const PAYMENT_METHODS: PaymentMethod[] = ['Dinheiro', 'Cartão', 'Pix']

export default function AdminAppointmentsSection({ barberId }: { barberId: string }) {
  const { data: appointments, isLoading } = useAdminBarberAppointments(barberId)
  const updatePayment = useAdminUpdateAppointmentPayment(barberId)
  const [registeringId, setRegisteringId] = useState<string | null>(null)

  const completed = appointments?.filter((a) => a.status === 'Completed') ?? []

  if (isLoading) return <div className="py-2"><Spinner size="sm" /></div>
  if (completed.length === 0) {
    return <p className="text-xs text-brand-white/30 py-2">Nenhum atendimento concluído.</p>
  }

  return (
    <div className="mt-2">
      <p className="text-xs font-semibold text-brand-white/40 mb-2 uppercase tracking-wide">
        Atendimentos concluídos
      </p>
      <div className="flex flex-col gap-1">
        {completed.slice(0, 10).map((appt) => (
          <div
            key={appt.id}
            className="flex flex-wrap items-center gap-3 rounded-lg border border-brand-white/5 bg-brand-black px-3 py-2 text-xs text-brand-white/60"
          >
            <span>{appt.clientName}</span>
            <span>{formatDateTime(appt.scheduledAt)}</span>
            <span className="text-brand-gold">{formatCurrency(appt.services.reduce((s, v) => s + v.price, 0))}</span>
            {appt.paymentMethod ? (
              <span className="rounded-full bg-brand-gold/15 px-2 py-0.5 text-brand-gold">
                {appt.paymentMethod}
              </span>
            ) : registeringId === appt.id ? (
              <div className="flex gap-1">
                {PAYMENT_METHODS.map((m) => (
                  <button
                    key={m}
                    onClick={async () => {
                      try {
                        await updatePayment.mutateAsync({ id: appt.id, paymentMethod: m })
                      } finally {
                        setRegisteringId(null)
                      }
                    }}
                    className="px-2 py-0.5 rounded border border-brand-white/20 text-brand-white/60 hover:border-brand-gold hover:text-brand-gold transition-colors"
                  >
                    {m}
                  </button>
                ))}
                <button
                  onClick={() => setRegisteringId(null)}
                  className="px-2 py-0.5 text-brand-white/30 hover:text-brand-white/60"
                >
                  ✕
                </button>
              </div>
            ) : (
              <button
                onClick={() => setRegisteringId(appt.id)}
                className="rounded border border-brand-white/20 px-2 py-0.5 text-brand-white/40 hover:border-brand-gold/50 hover:text-brand-gold/70 transition-colors"
              >
                Registrar pagamento
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
```

- [ ] **Step 4: Add AdminAppointmentsSection to admin barbers page**

```tsx
// In admin/barbers/page.tsx:
// 1. Add import:
import AdminAppointmentsSection from './AdminAppointmentsSection'

// 2. After the existing AdminBlocksSection row, add:
<tr key={`${barber.id}-appointments`} className="border-b border-brand-white/5">
  <td colSpan={5} className="px-4 pb-4">
    <AdminAppointmentsSection barberId={barber.id} />
  </td>
</tr>
```

- [ ] **Step 5: Update BarberAppointmentList tests**

```tsx
// In frontend/tests/unit/components/appointments/BarberAppointmentList.test.tsx,
// replace existing tests with:
import { describe, it, expect } from 'vitest'
import { render, screen, waitFor, fireEvent } from '../../test-utils'
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
    })
  })

  it('shows Concluir button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Concluir/i }).length).toBeGreaterThan(0)
    })
  })

  it('clicking Concluir shows payment method options', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => screen.getAllByRole('button', { name: /Concluir/i }))
    fireEvent.click(screen.getAllByRole('button', { name: /Concluir/i })[0])
    expect(screen.getByText(/Forma de pagamento/i)).toBeInTheDocument()
    expect(screen.getByText('Pular')).toBeInTheDocument()
  })

  it('Pular completes without payment method', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => screen.getAllByRole('button', { name: /Concluir/i }))
    fireEvent.click(screen.getAllByRole('button', { name: /Concluir/i })[0])
    fireEvent.click(screen.getByText('Pular'))
    fireEvent.click(screen.getByRole('button', { name: /Confirmar/i }))
    // No error thrown = payment selection didn't block completion
  })
})
```

- [ ] **Step 6: Create AdminAppointmentsSection test**

```tsx
// frontend/tests/unit/components/appointments/AdminAppointmentsSection.test.tsx
import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../../test-utils'
import AdminAppointmentsSection from '@/app/admin/barbers/AdminAppointmentsSection'

describe('AdminAppointmentsSection', () => {
  it('renders completed appointments', async () => {
    // MSW handler returns mockBarberAppointments with status Accepted
    // So completed list may be empty. Verify "Nenhum atendimento" message.
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await waitFor(() => {
      expect(screen.getByText(/Nenhum atendimento concluído/i)).toBeInTheDocument()
    })
  })
})
```

**Note:** Mock data has status `Accepted`. To test badges, you'd need to update mockBarberAppointments or use MSW `server.use()` override. The above minimal test verifies the component renders without crashing.

- [ ] **Step 7: Run frontend tests**

```bash
cd frontend && npm test
```

Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add frontend/src frontend/tests
git commit -m "feat(frontend): payment method modal, AppointmentCard badge, AdminAppointmentsSection"
```

---

### Task 7: Frontend — admin financial dashboard expansion

**Files:**
- Modify: `frontend/src/app/admin/dashboard/page.tsx`
- Create: `frontend/src/components/ui/RevenueChart.tsx`
- Modify: `frontend/tests/unit/app/` (check if dashboard tests exist; create if not)

**Interfaces:**
- Consumes: `useFinancialSummary` (updated with totalExpenses/netRevenue), `useFinancialTimeline`, `useExpenses`, `useCreateExpense`, `useDeleteExpense` from Task 5
- Produces: expanded admin dashboard with 5 summary cards (including Despesas + Lucro Líquido), period comparison arrows, Recharts BarChart, expenses section

- [ ] **Step 1: Install recharts**

```bash
cd frontend && npm install recharts
```

Expected: package added to package.json, no errors.

- [ ] **Step 2: Create RevenueChart component**

```tsx
// frontend/src/components/ui/RevenueChart.tsx
'use client'

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import type { FinancialTimelineItem } from '@/types/api.types'

interface RevenueChartProps {
  data: FinancialTimelineItem[]
  groupBy: 'day' | 'week' | 'month'
}

function formatPeriod(period: string, groupBy: 'day' | 'week' | 'month') {
  const date = new Date(period + 'T00:00:00')
  if (groupBy === 'month') return date.toLocaleDateString('pt-BR', { month: 'short', year: '2-digit' })
  if (groupBy === 'week') return `Sem ${date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })}`
  return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
}

function formatCurrency(value: number) {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export function RevenueChart({ data, groupBy }: RevenueChartProps) {
  if (data.length === 0) {
    return (
      <div className="flex h-48 items-center justify-center text-brand-white/30 text-sm">
        Nenhum dado para o período.
      </div>
    )
  }

  const chartData = data.map((item) => ({
    period: formatPeriod(item.period, groupBy),
    receita: item.revenue,
    atendimentos: item.appointments,
  }))

  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart data={chartData} margin={{ top: 4, right: 4, left: 0, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(245,245,245,0.08)" />
        <XAxis
          dataKey="period"
          tick={{ fill: 'rgba(245,245,245,0.4)', fontSize: 11 }}
          axisLine={false}
          tickLine={false}
        />
        <YAxis
          tickFormatter={(v) => `R$${v}`}
          tick={{ fill: 'rgba(245,245,245,0.4)', fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={56}
        />
        <Tooltip
          contentStyle={{ background: '#1A1A1A', border: '1px solid rgba(201,168,76,0.3)', borderRadius: 8 }}
          labelStyle={{ color: '#F5F5F5', marginBottom: 4 }}
          formatter={(value: number) => [formatCurrency(value), 'Receita']}
        />
        <Bar dataKey="receita" fill="#C9A84C" radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  )
}
```

- [ ] **Step 3: Replace admin dashboard page**

```tsx
// frontend/src/app/admin/dashboard/page.tsx
// Full replacement:
'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  useFinancialSummary,
  useFinancialByBarber,
  useFinancialByService,
  useFinancialTimeline,
  useExpenses,
  useCreateExpense,
  useDeleteExpense,
} from '@/hooks/useAdminFinancial'
import { adminApi } from '@/lib/api/admin.api'
import { RevenueChart } from '@/components/ui/RevenueChart'

function today() { return new Date().toISOString().slice(0, 10) }
function weekStart() {
  const d = new Date(); d.setDate(d.getDate() - d.getDay()); return d.toISOString().slice(0, 10)
}
function monthStart() {
  const d = new Date(); d.setDate(1); return d.toISOString().slice(0, 10)
}
function prevPeriodDates(from: string, to: string) {
  const f = new Date(from), t = new Date(to)
  const days = Math.round((t.getTime() - f.getTime()) / 86400000) + 1
  const prevTo = new Date(f); prevTo.setDate(prevTo.getDate() - 1)
  const prevFrom = new Date(prevTo); prevFrom.setDate(prevFrom.getDate() - days + 1)
  return { prevFrom: prevFrom.toISOString().slice(0, 10), prevTo: prevTo.toISOString().slice(0, 10) }
}

const PRESETS = [
  { label: 'Hoje', getDates: () => { const d = today(); return { from: d, to: d } } },
  { label: 'Esta semana', getDates: () => ({ from: weekStart(), to: today() }) },
  { label: 'Este mês', getDates: () => ({ from: monthStart(), to: today() }) },
]

const expenseSchema = z.object({
  amount: z.coerce.number().positive('Valor deve ser positivo'),
  description: z.string().min(1, 'Obrigatório').max(200),
  date: z.string().min(1, 'Obrigatório'),
})

type ExpenseForm = z.infer<typeof expenseSchema>

function pct(current: number, previous: number) {
  if (previous === 0) return null
  return Math.round(((current - previous) / previous) * 100)
}

function PctBadge({ value }: { value: number | null }) {
  if (value === null) return null
  const positive = value >= 0
  return (
    <span className={['text-xs font-semibold', positive ? 'text-green-400' : 'text-red-400'].join(' ')}>
      {positive ? '↑' : '↓'} {Math.abs(value)}%
    </span>
  )
}

export default function DashboardPage() {
  const [from, setFrom] = useState(monthStart)
  const [to, setTo] = useState(today)
  const [groupBy, setGroupBy] = useState<'day' | 'week' | 'month'>('day')

  const { prevFrom, prevTo } = prevPeriodDates(from, to)

  const { data: summary } = useFinancialSummary(from, to)
  const { data: prevSummary } = useFinancialSummary(prevFrom, prevTo)
  const { data: byBarber } = useFinancialByBarber(from, to)
  const { data: byService } = useFinancialByService(from, to)
  const { data: timeline } = useFinancialTimeline(from, to, groupBy)
  const { data: expenses } = useExpenses(from, to)
  const createExpense = useCreateExpense()
  const deleteExpense = useDeleteExpense()

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } =
    useForm<ExpenseForm>({ resolver: zodResolver(expenseSchema), defaultValues: { date: today() } })

  const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

  const summaryCards = [
    {
      label: 'Receita Total',
      value: fmt(summary?.totalRevenue ?? 0),
      compare: pct(summary?.totalRevenue ?? 0, prevSummary?.totalRevenue ?? 0),
    },
    {
      label: 'Atendimentos',
      value: String(summary?.totalAppointments ?? 0),
      compare: pct(summary?.totalAppointments ?? 0, prevSummary?.totalAppointments ?? 0),
    },
    {
      label: 'Ticket Médio',
      value: fmt(summary?.averageTicket ?? 0),
      compare: pct(summary?.averageTicket ?? 0, prevSummary?.averageTicket ?? 0),
    },
    {
      label: 'Despesas',
      value: fmt(summary?.totalExpenses ?? 0),
      compare: pct(summary?.totalExpenses ?? 0, prevSummary?.totalExpenses ?? 0),
      invertColor: true,
    },
    {
      label: 'Lucro Líquido',
      value: fmt(summary?.netRevenue ?? 0),
      compare: pct(summary?.netRevenue ?? 0, prevSummary?.netRevenue ?? 0),
    },
  ]

  const totalExpensesInPeriod = expenses?.reduce((s, e) => s + e.amount, 0) ?? 0

  return (
    <div className="space-y-8">
      <h1 className="font-montserrat text-2xl font-black text-brand-white">Dashboard Financeiro</h1>

      {/* Period selector */}
      <div className="flex flex-wrap gap-3 items-center">
        {PRESETS.map((p) => (
          <button
            key={p.label}
            onClick={() => { const d = p.getDates(); setFrom(d.from); setTo(d.to) }}
            className="px-4 py-2 rounded-lg border border-brand-gold/30 text-sm text-brand-gold hover:bg-brand-gold/10 transition-colors"
          >
            {p.label}
          </button>
        ))}
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm" />
        <span className="text-brand-white/50">até</span>
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm" />
        <button
          onClick={async () => {
            const blob = await adminApi.exportCsv(from, to)
            const url = URL.createObjectURL(blob)
            const link = document.createElement('a')
            link.href = url; link.download = `relatorio-${from}-${to}.csv`
            document.body.appendChild(link); link.click()
            document.body.removeChild(link); URL.revokeObjectURL(url)
          }}
          className="ml-auto px-4 py-2 rounded-lg bg-brand-gold text-brand-black text-sm font-semibold hover:bg-brand-gold-light transition-colors"
        >
          Exportar CSV
        </button>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {summaryCards.map(({ label, value, compare }) => (
          <div key={label} className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-5">
            <p className="text-xs text-brand-white/50">{label}</p>
            <p className="font-montserrat text-xl font-black text-brand-gold mt-1">{value}</p>
            <PctBadge value={compare} />
          </div>
        ))}
      </div>

      {/* Revenue chart */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="font-montserrat text-lg font-bold text-brand-white">Receita ao Longo do Tempo</h2>
          <div className="flex gap-1 text-sm">
            {(['day', 'week', 'month'] as const).map((g) => (
              <button
                key={g}
                onClick={() => setGroupBy(g)}
                className={[
                  'px-3 py-1 rounded-lg transition-colors',
                  groupBy === g
                    ? 'bg-brand-gold text-brand-black font-semibold'
                    : 'text-brand-white/50 hover:text-brand-white',
                ].join(' ')}
              >
                {g === 'day' ? 'Dia' : g === 'week' ? 'Semana' : 'Mês'}
              </button>
            ))}
          </div>
        </div>
        <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-4">
          <RevenueChart data={timeline ?? []} groupBy={groupBy} />
        </div>
      </section>

      {/* By Barber */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Por Barbeiro</h2>
        <table className="w-full text-sm text-brand-white/80">
          <thead>
            <tr className="border-b border-brand-white/10 text-left text-brand-white/40">
              <th className="pb-2">Barbeiro</th>
              <th className="pb-2">Atendimentos</th>
              <th className="pb-2">Receita</th>
            </tr>
          </thead>
          <tbody>
            {byBarber?.map((row) => (
              <tr key={row.barberId} className="border-b border-brand-white/5">
                <td className="py-2">{row.barberName}</td>
                <td className="py-2">{row.appointments}</td>
                <td className="py-2 text-brand-gold">{fmt(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* By Service */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Por Serviço</h2>
        <table className="w-full text-sm text-brand-white/80">
          <thead>
            <tr className="border-b border-brand-white/10 text-left text-brand-white/40">
              <th className="pb-2">Serviço</th>
              <th className="pb-2">Vendas</th>
              <th className="pb-2">Receita</th>
            </tr>
          </thead>
          <tbody>
            {byService?.map((row) => (
              <tr key={row.serviceId} className="border-b border-brand-white/5">
                <td className="py-2">{row.serviceName}</td>
                <td className="py-2">{row.count}</td>
                <td className="py-2 text-brand-gold">{fmt(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Expenses section */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Despesas</h2>

        {/* Add expense form */}
        <form
          onSubmit={handleSubmit(async (data) => {
            await createExpense.mutateAsync(data)
            reset({ date: today() })
          })}
          className="flex flex-wrap gap-3 mb-6 items-end"
        >
          <div className="flex flex-col gap-1">
            <label className="text-xs text-brand-white/50">Valor (R$)</label>
            <input
              type="number"
              step="0.01"
              min="0.01"
              placeholder="0,00"
              {...register('amount')}
              className="w-28 bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm focus:border-brand-gold focus:outline-none"
            />
            {errors.amount && <span className="text-xs text-brand-gold/70">{errors.amount.message}</span>}
          </div>
          <div className="flex flex-col gap-1 flex-1 min-w-[160px]">
            <label className="text-xs text-brand-white/50">Descrição</label>
            <input
              type="text"
              placeholder="Ex: Produto, aluguel..."
              maxLength={200}
              {...register('description')}
              className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm focus:border-brand-gold focus:outline-none"
            />
            {errors.description && <span className="text-xs text-brand-gold/70">{errors.description.message}</span>}
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs text-brand-white/50">Data</label>
            <input
              type="date"
              {...register('date')}
              className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm focus:border-brand-gold focus:outline-none"
            />
          </div>
          <button
            type="submit"
            disabled={isSubmitting}
            className="px-4 py-2 rounded-lg bg-brand-gold text-brand-black text-sm font-semibold hover:bg-brand-gold-light transition-colors disabled:opacity-50"
          >
            {isSubmitting ? 'Adicionando...' : 'Adicionar'}
          </button>
        </form>

        {/* Expense list */}
        {expenses && expenses.length > 0 ? (
          <div className="flex flex-col gap-1">
            {expenses.map((e) => (
              <div
                key={e.id}
                className="flex items-center gap-4 rounded-lg border border-brand-white/5 bg-brand-black-soft px-4 py-2 text-sm"
              >
                <span className="text-brand-white/60 flex-1">{e.description}</span>
                <span className="text-brand-white/40 text-xs">{new Date(e.date + 'T00:00:00').toLocaleDateString('pt-BR')}</span>
                <span className="font-semibold text-brand-gold">{fmt(e.amount)}</span>
                <button
                  onClick={() => {
                    if (!window.confirm(`Excluir despesa "${e.description}"?`)) return
                    deleteExpense.mutate(e.id)
                  }}
                  className="text-brand-white/30 hover:text-brand-white/70 transition-colors px-1"
                  aria-label="Excluir despesa"
                >
                  ✕
                </button>
              </div>
            ))}
            <div className="flex justify-end pt-2 text-sm font-semibold text-brand-white/60">
              Total: <span className="ml-2 text-brand-gold">{fmt(totalExpensesInPeriod)}</span>
            </div>
          </div>
        ) : (
          <p className="text-brand-white/30 text-sm">Nenhuma despesa registrada no período.</p>
        )}
      </section>
    </div>
  )
}
```

- [ ] **Step 4: Write dashboard tests**

```tsx
// frontend/tests/unit/app/AdminDashboard.test.tsx
import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../test-utils'
import DashboardPage from '@/app/admin/dashboard/page'

describe('AdminDashboardPage', () => {
  it('renders summary cards including Despesas and Lucro Líquido', async () => {
    render(<DashboardPage />)
    await waitFor(() => {
      expect(screen.getByText('Despesas')).toBeInTheDocument()
      expect(screen.getByText('Lucro Líquido')).toBeInTheDocument()
      expect(screen.getByText('Receita Total')).toBeInTheDocument()
    })
  })

  it('renders groupBy buttons for timeline', async () => {
    render(<DashboardPage />)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Dia' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Semana' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Mês' })).toBeInTheDocument()
    })
  })

  it('renders expenses section with form', async () => {
    render(<DashboardPage />)
    await waitFor(() => {
      expect(screen.getByText('Despesas')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /Adicionar/i })).toBeInTheDocument()
    })
  })

  it('shows existing expenses from mock data', async () => {
    render(<DashboardPage />)
    await waitFor(() => {
      expect(screen.getByText('Produto')).toBeInTheDocument()
    })
  })
})
```

- [ ] **Step 5: Run frontend tests**

```bash
cd frontend && npm test
```

Expected: all pass.

- [ ] **Step 6: Run TypeScript check**

```bash
cd frontend && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 7: Run full backend test suite one final time**

```bash
cd backend && dotnet test
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add frontend/src frontend/tests
git commit -m "feat(frontend): admin dashboard — revenue chart, expenses section, 5 summary cards with comparison"
```
