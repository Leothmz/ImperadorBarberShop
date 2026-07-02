# Agenda Blocks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow barbers (and admins) to manually block periods on their schedule, preventing clients from booking those slots.

**Architecture:** New `BarberBlock` domain entity with bitmask recurrence; `IBarberBlockRepository` in Domain; EF config + migration; MediatR CQRS handlers co-located per file (record + validator + handler); barber endpoints on `BarbersController`, admin endpoints on `AdminController`; `GetAvailableSlotsQuery` updated to filter blocks; frontend tabs on barber dashboard and admin barbers page.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, MediatR, FluentValidation, xUnit + NSubstitute + FluentAssertions + Testcontainers, Next.js 15 App Router, TanStack Query v5, React Hook Form + Zod, Tailwind CSS v4.

## Global Constraints

- Clean Architecture: Domain has zero external deps; Application depends only on Domain; Infrastructure and Api depend on Application
- All handler files are co-located: one `.cs` file contains the record + AbstractValidator + IRequestHandler
- All admin endpoints require `[Authorize(Policy = "RequireAdminRole")]`; barber endpoints require `[Authorize(Policy = "RequireBarberRole")]`
- IDOR: barber endpoints extract `BarberId` from JWT claim `barberId`, never from request body
- Recurrence bitmask: Dom=1, Seg=2, Ter=4, Qua=8, Qui=16, Sex=32, Sáb=64; max value = 127
- `StartsAt < EndsAt` always enforced
- If `IsRecurring = false`: `RecurrenceDays` and `RecurrenceEndsAt` must be null
- If `IsRecurring = true`: `RecurrenceDays` must be between 1 and 127
- Slot overlap check: `slotStart < blockEnd && slotEnd > blockStart`
- All UI text in Brazilian Portuguese
- Brand colors: `brand-gold` (#C9A84C), `brand-black` (#0D0D0D), `brand-black-soft` (#1A1A1A), `brand-white` (#F5F5F5)

---

### Task 1: Domain Entity

**Files:**
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/BarberBlock.cs`

**Interfaces:**
- Produces: `BarberBlock` entity with factory `Create(...)` and properties used by Tasks 2–5

- [ ] **Step 1: Create the entity**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Entities/BarberBlock.cs
namespace ImperadorBarberShop.Domain.Entities;

public class BarberBlock
{
    public Guid Id { get; private set; }
    public Guid BarberId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string? Description { get; private set; }
    public bool IsRecurring { get; private set; }
    public int? RecurrenceDays { get; private set; }
    public DateTime? RecurrenceEndsAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BarberBlock() { }

    public static BarberBlock Create(
        Guid barberId,
        DateTime startsAt,
        DateTime endsAt,
        string? description,
        bool isRecurring,
        int? recurrenceDays,
        DateTime? recurrenceEndsAt)
    {
        return new BarberBlock
        {
            Id = Guid.NewGuid(),
            BarberId = barberId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Description = description,
            IsRecurring = isRecurring,
            RecurrenceDays = isRecurring ? recurrenceDays : null,
            RecurrenceEndsAt = isRecurring ? recurrenceEndsAt : null,
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>Returns true if this block is active on the given UTC date.</summary>
    public bool IsActiveOn(DateOnly date)
    {
        if (!IsRecurring)
            return DateOnly.FromDateTime(StartsAt) == date;

        var dayBit = 1 << (int)date.DayOfWeek; // Sun=1,Mon=2,...,Sat=64
        if ((RecurrenceDays & dayBit) == 0)
            return false;

        if (RecurrenceEndsAt.HasValue && date > DateOnly.FromDateTime(RecurrenceEndsAt.Value))
            return false;

        return true;
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
cd backend && dotnet build
```
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Entities/BarberBlock.cs
git commit -m "feat(domain): BarberBlock entity with recurrence bitmask"
```

---

### Task 2: Repository Interface + Infrastructure (EF config, repository, migration, DI)

**Files:**
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IBarberBlockRepository.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/BarberBlockConfiguration.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/BarberBlockRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<BarberBlock>`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs` — register `IBarberBlockRepository`
- Generate migration: `AddBarberBlocks`

**Interfaces:**
- Consumes: `BarberBlock` entity from Task 1
- Produces:
  ```csharp
  Task<List<BarberBlock>> GetByBarberIdAsync(Guid barberId, CancellationToken ct = default);
  Task<BarberBlock?> GetByIdAsync(Guid id, CancellationToken ct = default);
  Task AddAsync(BarberBlock block, CancellationToken ct = default);
  Task DeleteAsync(BarberBlock block, CancellationToken ct = default);
  Task<List<BarberBlock>> GetActiveOnDateAsync(Guid barberId, DateOnly date, CancellationToken ct = default);
  ```

- [ ] **Step 1: Create the repository interface**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IBarberBlockRepository.cs
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IBarberBlockRepository
{
    Task<List<BarberBlock>> GetByBarberIdAsync(Guid barberId, CancellationToken ct = default);
    Task<BarberBlock?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(BarberBlock block, CancellationToken ct = default);
    Task DeleteAsync(BarberBlock block, CancellationToken ct = default);
    /// <summary>Returns all blocks active on the given date (pontual matching date, or recurring matching day-of-week and within recurrenceEndsAt).</summary>
    Task<List<BarberBlock>> GetActiveOnDateAsync(Guid barberId, DateOnly date, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create EF configuration**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/BarberBlockConfiguration.cs
using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class BarberBlockConfiguration : IEntityTypeConfiguration<BarberBlock>
{
    public void Configure(EntityTypeBuilder<BarberBlock> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BarberId).IsRequired();
        builder.Property(b => b.StartsAt).IsRequired();
        builder.Property(b => b.EndsAt).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(200);
        builder.Property(b => b.IsRecurring).IsRequired();
        builder.Property(b => b.RecurrenceDays);
        builder.Property(b => b.RecurrenceEndsAt);
        builder.Property(b => b.CreatedAt).IsRequired();

        builder.HasIndex(b => b.BarberId);
    }
}
```

- [ ] **Step 3: Create repository implementation**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/BarberBlockRepository.cs
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class BarberBlockRepository : IBarberBlockRepository
{
    private readonly AppDbContext _context;

    public BarberBlockRepository(AppDbContext context) => _context = context;

    public async Task<List<BarberBlock>> GetByBarberIdAsync(Guid barberId, CancellationToken ct = default)
        => await _context.BarberBlocks
            .Where(b => b.BarberId == barberId)
            .OrderBy(b => b.StartsAt)
            .ToListAsync(ct);

    public async Task<BarberBlock?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.BarberBlocks.FindAsync([id], ct);

    public async Task AddAsync(BarberBlock block, CancellationToken ct = default)
        => await _context.BarberBlocks.AddAsync(block, ct);

    public Task DeleteAsync(BarberBlock block, CancellationToken ct = default)
    {
        _context.BarberBlocks.Remove(block);
        return Task.CompletedTask;
    }

    public async Task<List<BarberBlock>> GetActiveOnDateAsync(Guid barberId, DateOnly date, CancellationToken ct = default)
    {
        var dayBit = 1 << (int)date.DayOfWeek;
        var dateAsDateTime = date.ToDateTime(TimeOnly.MinValue);
        var nextDay = dateAsDateTime.AddDays(1);

        return await _context.BarberBlocks
            .Where(b => b.BarberId == barberId && (
                // Pontual: StartsAt falls on the requested date
                (!b.IsRecurring && b.StartsAt >= dateAsDateTime && b.StartsAt < nextDay)
                ||
                // Recorrente: day-of-week bit matches AND within recurrenceEndsAt
                (b.IsRecurring
                    && (b.RecurrenceDays & dayBit) != 0
                    && (b.RecurrenceEndsAt == null || b.RecurrenceEndsAt >= dateAsDateTime))
            ))
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 4: Add DbSet to AppDbContext**

In `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContext.cs`, add:

```csharp
public DbSet<BarberBlock> BarberBlocks => Set<BarberBlock>();
```

- [ ] **Step 5: Register in DI**

In `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`, inside the repositories section, add:

```csharp
services.AddScoped<IBarberBlockRepository, BarberBlockRepository>();
```

- [ ] **Step 6: Generate and apply migration**

```bash
cd backend
dotnet ef migrations add AddBarberBlocks \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api

dotnet ef database update \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```

Expected: migration file created, database updated successfully.

- [ ] **Step 7: Build to verify**

```bash
cd backend && dotnet build
```
Expected: 0 errors

- [ ] **Step 8: Commit**

```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IBarberBlockRepository.cs
git add backend/src/Infrastructure/
git commit -m "feat(infra): BarberBlock repository, EF config, migration, DI"
```

---

### Task 3: Barber CRUD Handlers + Controller Endpoints

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Blocks/CreateBarberBlockCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Blocks/DeleteBarberBlockCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Blocks/GetBarberBlocksQuery.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/BarbersController.cs` — add 3 endpoints
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Blocks/CreateBarberBlockCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IBarberBlockRepository` from Task 2, `IUnitOfWork` (already exists)
- Produces: 3 endpoints: `GET /api/v1/barbers/me/blocks`, `POST /api/v1/barbers/me/blocks`, `DELETE /api/v1/barbers/me/blocks/{id}`

- [ ] **Step 1: Write failing unit test**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Blocks/CreateBarberBlockCommandHandlerTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Blocks;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Blocks;

public class CreateBarberBlockCommandHandlerTests
{
    private readonly IBarberBlockRepository _repo = Substitute.For<IBarberBlockRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateBarberBlockCommandHandler _handler;

    public CreateBarberBlockCommandHandlerTests()
    {
        _handler = new CreateBarberBlockCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_PontualBlock_AddsAndSaves()
    {
        var barberId = Guid.NewGuid();
        var cmd = new CreateBarberBlockCommand(
            barberId,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            "Almoço",
            false, null, null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<BarberBlock>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RecurringBlock_SetsRecurrenceDays()
    {
        var barberId = Guid.NewGuid();
        var cmd = new CreateBarberBlockCommand(
            barberId,
            new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 7, 13, 0, 0, DateTimeKind.Utc),
            null,
            true, 42, null);

        await _handler.Handle(cmd, CancellationToken.None);

        await _repo.Received(1).AddAsync(
            Arg.Is<BarberBlock>(b => b.IsRecurring && b.RecurrenceDays == 42),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "CreateBarberBlock"
```
Expected: FAIL — type not found

- [ ] **Step 3: Create CreateBarberBlockCommand**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Blocks/CreateBarberBlockCommand.cs
using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Blocks;

public record CreateBarberBlockCommand(
    Guid BarberId,
    DateTime StartsAt,
    DateTime EndsAt,
    string? Description,
    bool IsRecurring,
    int? RecurrenceDays,
    DateTime? RecurrenceEndsAt) : IRequest<Guid>;

public class CreateBarberBlockCommandValidator : AbstractValidator<CreateBarberBlockCommand>
{
    public CreateBarberBlockCommandValidator()
    {
        RuleFor(x => x.BarberId).NotEmpty();
        RuleFor(x => x.StartsAt).NotEmpty();
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt)
            .WithMessage("EndsAt must be after StartsAt.");
        RuleFor(x => x.Description).MaximumLength(200).When(x => x.Description is not null);
        When(x => x.IsRecurring, () =>
        {
            RuleFor(x => x.RecurrenceDays).NotNull()
                .InclusiveBetween(1, 127)
                .WithMessage("RecurrenceDays must be between 1 and 127 for recurring blocks.");
        });
        When(x => !x.IsRecurring, () =>
        {
            RuleFor(x => x.RecurrenceDays).Null()
                .WithMessage("RecurrenceDays must be null for non-recurring blocks.");
        });
    }
}

public class CreateBarberBlockCommandHandler : IRequestHandler<CreateBarberBlockCommand, Guid>
{
    private readonly IBarberBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateBarberBlockCommandHandler(IBarberBlockRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateBarberBlockCommand request, CancellationToken cancellationToken)
    {
        var block = BarberBlock.Create(
            request.BarberId,
            request.StartsAt,
            request.EndsAt,
            request.Description,
            request.IsRecurring,
            request.RecurrenceDays,
            request.RecurrenceEndsAt);

        await _repo.AddAsync(block, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return block.Id;
    }
}
```

- [ ] **Step 4: Create DeleteBarberBlockCommand**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Blocks/DeleteBarberBlockCommand.cs
using FluentValidation;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Blocks;

public record DeleteBarberBlockCommand(Guid BlockId, Guid BarberId) : IRequest;

public class DeleteBarberBlockCommandValidator : AbstractValidator<DeleteBarberBlockCommand>
{
    public DeleteBarberBlockCommandValidator()
    {
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
    }
}

public class DeleteBarberBlockCommandHandler : IRequestHandler<DeleteBarberBlockCommand>
{
    private readonly IBarberBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeleteBarberBlockCommandHandler(IBarberBlockRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(DeleteBarberBlockCommand request, CancellationToken cancellationToken)
    {
        var block = await _repo.GetByIdAsync(request.BlockId, cancellationToken);
        if (block is null)
            throw new KeyNotFoundException($"Block '{request.BlockId}' not found.");

        if (block.BarberId != request.BarberId)
            throw new ForbiddenException("You are not authorized to delete this block.");

        await _repo.DeleteAsync(block, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Create GetBarberBlocksQuery**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Queries/Blocks/GetBarberBlocksQuery.cs
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Blocks;

public record BarberBlockDto(
    Guid Id,
    DateTime StartsAt,
    DateTime EndsAt,
    string? Description,
    bool IsRecurring,
    int? RecurrenceDays,
    DateTime? RecurrenceEndsAt,
    DateTime CreatedAt);

public record GetBarberBlocksQuery(Guid BarberId) : IRequest<List<BarberBlockDto>>;

public class GetBarberBlocksQueryHandler : IRequestHandler<GetBarberBlocksQuery, List<BarberBlockDto>>
{
    private readonly IBarberBlockRepository _repo;

    public GetBarberBlocksQueryHandler(IBarberBlockRepository repo) => _repo = repo;

    public async Task<List<BarberBlockDto>> Handle(GetBarberBlocksQuery request, CancellationToken cancellationToken)
    {
        var blocks = await _repo.GetByBarberIdAsync(request.BarberId, cancellationToken);
        return blocks.Select(b => new BarberBlockDto(
            b.Id, b.StartsAt, b.EndsAt, b.Description,
            b.IsRecurring, b.RecurrenceDays, b.RecurrenceEndsAt, b.CreatedAt))
            .ToList();
    }
}
```

- [ ] **Step 6: Add endpoints to BarbersController**

Add these imports to `BarbersController.cs` (top of file):
```csharp
using ImperadorBarberShop.Application.Commands.Blocks;
using ImperadorBarberShop.Application.Queries.Blocks;
```

Add these methods inside the controller class (after existing endpoints):

```csharp
[HttpGet("me/blocks")]
[Authorize(Policy = "RequireBarberRole")]
public async Task<IActionResult> GetMyBlocks(CancellationToken ct)
{
    var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
    var result = await _mediator.Send(new GetBarberBlocksQuery(barberId), ct);
    return Ok(result);
}

[HttpPost("me/blocks")]
[Authorize(Policy = "RequireBarberRole")]
public async Task<IActionResult> CreateBlock([FromBody] CreateBarberBlockBody body, CancellationToken ct)
{
    var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
    var id = await _mediator.Send(new CreateBarberBlockCommand(
        barberId, body.StartsAt, body.EndsAt, body.Description,
        body.IsRecurring, body.RecurrenceDays, body.RecurrenceEndsAt), ct);
    return CreatedAtAction(nameof(GetMyBlocks), new { }, new { id });
}

[HttpDelete("me/blocks/{id:guid}")]
[Authorize(Policy = "RequireBarberRole")]
public async Task<IActionResult> DeleteBlock(Guid id, CancellationToken ct)
{
    var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
    await _mediator.Send(new DeleteBarberBlockCommand(id, barberId), ct);
    return NoContent();
}

public record CreateBarberBlockBody(
    DateTime StartsAt,
    DateTime EndsAt,
    string? Description,
    bool IsRecurring,
    int? RecurrenceDays,
    DateTime? RecurrenceEndsAt);
```

Also add `using System.Security.Claims;` if not already present.

- [ ] **Step 7: Run tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "CreateBarberBlock"
```
Expected: 2 passed

- [ ] **Step 8: Build**

```bash
cd backend && dotnet build
```
Expected: 0 errors

- [ ] **Step 9: Commit**

```bash
git add backend/src/Application/ImperadorBarberShop.Application/Commands/Blocks/
git add backend/src/Application/ImperadorBarberShop.Application/Queries/Blocks/
git add backend/src/Api/ImperadorBarberShop.Api/Controllers/BarbersController.cs
git add backend/tests/ImperadorBarberShop.UnitTests/Blocks/
git commit -m "feat(api): barber block CRUD — GET/POST/DELETE /barbers/me/blocks"
```

---

### Task 4: Admin Block Endpoints

**Files:**
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs` — add 3 endpoints
- Create: `backend/tests/ImperadorBarberShop.IntegrationTests/Admin/AdminBlocksControllerTests.cs`

**Interfaces:**
- Consumes: `GetBarberBlocksQuery`, `CreateBarberBlockCommand`, `DeleteBarberBlockCommand` from Task 3; `WebAppFixture` (existing)
- Produces: `GET /api/v1/admin/barbers/{barberId}/blocks`, `POST /api/v1/admin/barbers/{barberId}/blocks`, `DELETE /api/v1/admin/barbers/{barberId}/blocks/{id}`

- [ ] **Step 1: Add admin endpoints to AdminController**

Add these imports to `AdminController.cs`:
```csharp
using ImperadorBarberShop.Application.Commands.Blocks;
using ImperadorBarberShop.Application.Queries.Blocks;
```

Add these methods inside `AdminController`:

```csharp
[HttpGet("barbers/{barberId:guid}/blocks")]
public async Task<IActionResult> GetBarberBlocks(Guid barberId, CancellationToken ct)
    => Ok(await _mediator.Send(new GetBarberBlocksQuery(barberId), ct));

[HttpPost("barbers/{barberId:guid}/blocks")]
public async Task<IActionResult> CreateBarberBlock(
    Guid barberId,
    [FromBody] CreateBarberBlockBody body,
    CancellationToken ct)
{
    var id = await _mediator.Send(new CreateBarberBlockCommand(
        barberId, body.StartsAt, body.EndsAt, body.Description,
        body.IsRecurring, body.RecurrenceDays, body.RecurrenceEndsAt), ct);
    return CreatedAtAction(nameof(GetBarberBlocks), new { barberId }, new { id });
}

[HttpDelete("barbers/{barberId:guid}/blocks/{id:guid}")]
public async Task<IActionResult> DeleteBarberBlock(Guid barberId, Guid id, CancellationToken ct)
{
    await _mediator.Send(new DeleteBarberBlockCommand(id, barberId), ct);
    return NoContent();
}
```

Note: `CreateBarberBlockBody` is already defined in `BarbersController.cs`. Move it to a shared namespace or duplicate it here with a different name `AdminCreateBarberBlockBody` — the simplest approach is to duplicate the record in AdminController:

```csharp
public record AdminCreateBarberBlockBody(
    DateTime StartsAt,
    DateTime EndsAt,
    string? Description,
    bool IsRecurring,
    int? RecurrenceDays,
    DateTime? RecurrenceEndsAt);
```

And update the `CreateBarberBlock` admin action to use `AdminCreateBarberBlockBody`.

- [ ] **Step 2: Write integration tests**

```csharp
// backend/tests/ImperadorBarberShop.IntegrationTests/Admin/AdminBlocksControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Blocks;

namespace ImperadorBarberShop.IntegrationTests.Admin;

public class AdminBlocksControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;

    public AdminBlocksControllerTests(WebAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetBarberBlocks_AsAdmin_Returns200()
    {
        await _fixture.SeedBarberAsync("Bloco Barber", "bloco@test.com");
        var barbers = await (await _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid())
            .GetAsync("/api/v1/admin/barbers")).Content.ReadFromJsonAsync<List<dynamic>>();
        // Just test that a valid barberId returns 200 with empty list
        var client = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var barberId = Guid.NewGuid(); // arbitrary — endpoint returns empty list for unknown
        var response = await client.GetAsync($"/api/v1/admin/barbers/{barberId}/blocks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateAndDeleteBlock_AsAdmin_Works()
    {
        var barberId = Guid.NewGuid();
        var client = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/barbers/{barberId}/blocks",
            new
            {
                startsAt = DateTime.UtcNow.AddDays(1).Date.AddHours(12),
                endsAt = DateTime.UtcNow.AddDays(1).Date.AddHours(13),
                description = "Almoço",
                isRecurring = false,
                recurrenceDays = (int?)null,
                recurrenceEndsAt = (DateTime?)null
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        var id = (Guid)created!.id;
        var deleteResponse = await client.DeleteAsync($"/api/v1/admin/barbers/{barberId}/blocks/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetBarberBlocks_Unauthenticated_Returns401()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/admin/barbers/{Guid.NewGuid()}/blocks");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 3: Run integration tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests --filter "AdminBlocks"
```
Expected: 3 passed

- [ ] **Step 4: Commit**

```bash
git add backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs
git add backend/tests/ImperadorBarberShop.IntegrationTests/Admin/AdminBlocksControllerTests.cs
git commit -m "feat(api): admin block endpoints + integration tests"
```

---

### Task 5: Update GetAvailableSlotsQuery to Filter Blocks

**Files:**
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Queries/Barbers/GetAvailableSlotsQuery.cs` — inject `IBarberBlockRepository`, filter blocked slots
- Modify: `backend/tests/ImperadorBarberShop.UnitTests/Barbers/GetAvailableSlotsQueryHandlerTests.cs` (create if doesn't exist)

**Interfaces:**
- Consumes: `IBarberBlockRepository.GetActiveOnDateAsync(barberId, date)` from Task 2

- [ ] **Step 1: Write failing test**

Create or add to `backend/tests/ImperadorBarberShop.UnitTests/Barbers/GetAvailableSlotsQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Barbers;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Barbers;

public class GetAvailableSlotsQueryHandlerTests
{
    private readonly IBarberAvailabilityRepository _availRepo = Substitute.For<IBarberAvailabilityRepository>();
    private readonly IAppointmentRepository _apptRepo = Substitute.For<IAppointmentRepository>();
    private readonly IServiceRepository _svcRepo = Substitute.For<IServiceRepository>();
    private readonly IBarberBlockRepository _blockRepo = Substitute.For<IBarberBlockRepository>();
    private readonly GetAvailableSlotsQueryHandler _handler;

    public GetAvailableSlotsQueryHandlerTests()
    {
        _handler = new GetAvailableSlotsQueryHandler(_availRepo, _apptRepo, _svcRepo, _blockRepo);
    }

    [Fact]
    public async Task Handle_SlotOverlapsBlock_ExcludesSlot()
    {
        var barberId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 1); // a Friday
        var serviceId = Guid.NewGuid();

        _availRepo.GetByBarberIdAndDayAsync(barberId, DayOfWeek.Friday, Arg.Any<CancellationToken>())
            .Returns(BarberAvailability.Create(barberId, DayOfWeek.Friday,
                new TimeOnly(9, 0), new TimeOnly(17, 0)));

        _svcRepo.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { Service.Create("Corte", "desc", 30, 35m) });

        _apptRepo.GetActiveByBarberIdAndDateAsync(barberId, date, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        // Block from 09:00 to 10:00 — should exclude 09:00 and 09:15 slots
        var block = BarberBlock.Create(barberId,
            date.ToDateTime(new TimeOnly(9, 0)),
            date.ToDateTime(new TimeOnly(10, 0)),
            null, false, null, null);

        _blockRepo.GetActiveOnDateAsync(barberId, date, Arg.Any<CancellationToken>())
            .Returns(new List<BarberBlock> { block });

        var result = await _handler.Handle(
            new GetAvailableSlotsQuery(barberId, date, new List<Guid> { serviceId }),
            CancellationToken.None);

        result.Should().NotContain(new TimeOnly(9, 0));
        result.Should().NotContain(new TimeOnly(9, 15));
        result.Should().Contain(new TimeOnly(10, 0));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "GetAvailableSlots"
```
Expected: FAIL — constructor doesn't accept 4 args

- [ ] **Step 3: Update GetAvailableSlotsQuery handler**

Replace the full content of `GetAvailableSlotsQuery.cs`:

```csharp
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Barbers;

public record GetAvailableSlotsQuery(
    Guid BarberId,
    DateOnly Date,
    List<Guid> ServiceIds) : IRequest<List<TimeOnly>>;

public class GetAvailableSlotsQueryHandler : IRequestHandler<GetAvailableSlotsQuery, List<TimeOnly>>
{
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IBarberBlockRepository _blockRepository;

    public GetAvailableSlotsQueryHandler(
        IBarberAvailabilityRepository availabilityRepository,
        IAppointmentRepository appointmentRepository,
        IServiceRepository serviceRepository,
        IBarberBlockRepository blockRepository)
    {
        _availabilityRepository = availabilityRepository;
        _appointmentRepository = appointmentRepository;
        _serviceRepository = serviceRepository;
        _blockRepository = blockRepository;
    }

    public async Task<List<TimeOnly>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var dayOfWeek = request.Date.DayOfWeek;
        var availability = await _availabilityRepository.GetByBarberIdAndDayAsync(
            request.BarberId, dayOfWeek, cancellationToken);

        if (availability is null)
            return new List<TimeOnly>();

        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count == 0)
            return new List<TimeOnly>();

        var totalDuration = services.Sum(s => s.DurationMinutes);

        var activeAppointments = await _appointmentRepository.GetActiveByBarberIdAndDateAsync(
            request.BarberId, request.Date, cancellationToken);

        var blocks = await _blockRepository.GetActiveOnDateAsync(
            request.BarberId, request.Date, cancellationToken);

        // Build all occupied intervals (appointments + blocks)
        var occupiedBlocks = activeAppointments
            .Select(a => (Start: a.ScheduledAt, End: a.ScheduledAt.AddMinutes(a.TotalDurationMinutes)))
            .Concat(blocks.Select(b => (
                Start: request.Date.ToDateTime(TimeOnly.FromDateTime(b.StartsAt)),
                End: request.Date.ToDateTime(TimeOnly.FromDateTime(b.EndsAt)))))
            .ToList();

        var slots = new List<TimeOnly>();
        var current = availability.StartTime;
        var windowEnd = availability.EndTime.AddMinutes(-totalDuration);

        while (current <= windowEnd)
        {
            var slotStart = request.Date.ToDateTime(current);
            var slotEnd = slotStart.AddMinutes(totalDuration);

            var hasOverlap = occupiedBlocks.Any(block =>
                slotStart < block.End && slotEnd > block.Start);

            if (!hasOverlap && slotStart > DateTime.UtcNow)
                slots.Add(current);

            current = current.AddMinutes(15);
        }

        return slots;
    }
}
```

- [ ] **Step 4: Run test**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "GetAvailableSlots"
```
Expected: 1 passed

- [ ] **Step 5: Run full test suite**

```bash
cd backend && dotnet test
```
Expected: all pass

- [ ] **Step 6: Commit**

```bash
git add backend/src/Application/ImperadorBarberShop.Application/Queries/Barbers/GetAvailableSlotsQuery.cs
git add backend/tests/ImperadorBarberShop.UnitTests/Barbers/
git commit -m "feat(slots): exclude blocked intervals from available slots"
```

---

### Task 6: Frontend — Barber Dashboard Blocks Tab

**Files:**
- Create: `frontend/src/lib/api/blocks.api.ts`
- Create: `frontend/src/hooks/useBarberBlocks.ts`
- Modify: `frontend/src/app/barber/dashboard/page.tsx` — add "Bloqueios" tab
- Create: `frontend/tests/unit/app/barber/BlocksTab.test.tsx`

**Interfaces:**
- Consumes: `GET /api/v1/barbers/me/blocks`, `POST /api/v1/barbers/me/blocks`, `DELETE /api/v1/barbers/me/blocks/{id}`
- Produces: `useBarberBlocks`, `useCreateBarberBlock`, `useDeleteBarberBlock` hooks

- [ ] **Step 1: Create API client**

```typescript
// frontend/src/lib/api/blocks.api.ts
import { apiClient } from '@/lib/api/client'

export interface BarberBlockDto {
  id: string
  startsAt: string
  endsAt: string
  description: string | null
  isRecurring: boolean
  recurrenceDays: number | null
  recurrenceEndsAt: string | null
  createdAt: string
}

export interface CreateBarberBlockPayload {
  startsAt: string
  endsAt: string
  description?: string
  isRecurring: boolean
  recurrenceDays?: number | null
  recurrenceEndsAt?: string | null
}

export const blocksApi = {
  getMyBlocks: (): Promise<BarberBlockDto[]> =>
    apiClient.get('/barbers/me/blocks').then(r => r.data),

  createBlock: (payload: CreateBarberBlockPayload): Promise<{ id: string }> =>
    apiClient.post('/barbers/me/blocks', payload).then(r => r.data),

  deleteBlock: (id: string): Promise<void> =>
    apiClient.delete(`/barbers/me/blocks/${id}`).then(() => undefined),
}
```

- [ ] **Step 2: Create hooks**

```typescript
// frontend/src/hooks/useBarberBlocks.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { blocksApi, CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const BLOCKS_KEY = ['barber', 'blocks'] as const

export function useBarberBlocks() {
  return useQuery({ queryKey: BLOCKS_KEY, queryFn: blocksApi.getMyBlocks })
}

export function useCreateBarberBlock() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBarberBlockPayload) => blocksApi.createBlock(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: BLOCKS_KEY }),
  })
}

export function useDeleteBarberBlock() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => blocksApi.deleteBlock(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: BLOCKS_KEY }),
  })
}
```

- [ ] **Step 3: Write failing test**

```typescript
// frontend/tests/unit/app/barber/BlocksTab.test.tsx
import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'

// We'll test a standalone BlocksTab component (to be created)
import BlocksTab from '@/app/barber/dashboard/BlocksTab'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

beforeEach(() => {
  server.use(
    http.get('*/barbers/me/blocks', () =>
      HttpResponse.json([
        {
          id: 'block-1',
          startsAt: '2026-08-01T12:00:00Z',
          endsAt: '2026-08-01T13:00:00Z',
          description: 'Almoço',
          isRecurring: false,
          recurrenceDays: null,
          recurrenceEndsAt: null,
          createdAt: '2026-07-01T00:00:00Z',
        },
      ])
    ),
    http.delete('*/barbers/me/blocks/*', () => new HttpResponse(null, { status: 204 }))
  )
})

describe('BlocksTab', () => {
  it('renders existing blocks', async () => {
    render(<BlocksTab />, { wrapper })
    expect(await screen.findByText('Almoço')).toBeInTheDocument()
  })

  it('renders add block button', () => {
    render(<BlocksTab />, { wrapper })
    expect(screen.getByRole('button', { name: /adicionar bloqueio/i })).toBeInTheDocument()
  })

  it('opens modal on add click', () => {
    render(<BlocksTab />, { wrapper })
    fireEvent.click(screen.getByRole('button', { name: /adicionar bloqueio/i }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
```

- [ ] **Step 4: Run test to verify it fails**

```bash
cd frontend && npm test -- --reporter=verbose tests/unit/app/barber/BlocksTab.test.tsx
```
Expected: FAIL — module not found

- [ ] **Step 5: Create BlocksTab component**

```tsx
// frontend/src/app/barber/dashboard/BlocksTab.tsx
'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useBarberBlocks, useCreateBarberBlock, useDeleteBarberBlock } from '@/hooks/useBarberBlocks'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'
import type { CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const DAYS = [
  { label: 'Dom', bit: 1 },
  { label: 'Seg', bit: 2 },
  { label: 'Ter', bit: 4 },
  { label: 'Qua', bit: 8 },
  { label: 'Qui', bit: 16 },
  { label: 'Sex', bit: 32 },
  { label: 'Sáb', bit: 64 },
]

const blockSchema = z.object({
  startsAt: z.string().min(1, 'Obrigatório'),
  endsAt: z.string().min(1, 'Obrigatório'),
  description: z.string().max(200).optional(),
  isRecurring: z.boolean(),
  selectedDays: z.number().min(0).max(127).optional(),
  recurrenceEndsAt: z.string().optional(),
}).refine(d => d.endsAt > d.startsAt, {
  message: 'Fim deve ser após início',
  path: ['endsAt'],
}).refine(d => !d.isRecurring || (d.selectedDays && d.selectedDays > 0), {
  message: 'Selecione ao menos um dia',
  path: ['selectedDays'],
})

type BlockFormValues = z.infer<typeof blockSchema>

export default function BlocksTab() {
  const { data: blocks, isLoading } = useBarberBlocks()
  const createBlock = useCreateBarberBlock()
  const deleteBlock = useDeleteBarberBlock()
  const [open, setOpen] = useState(false)

  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } =
    useForm<BlockFormValues>({
      resolver: zodResolver(blockSchema),
      defaultValues: { isRecurring: false, selectedDays: 0 },
    })

  const isRecurring = watch('isRecurring')
  const selectedDays = watch('selectedDays') ?? 0

  const toggleDay = (bit: number) => {
    setValue('selectedDays', selectedDays ^ bit)
  }

  const onSubmit = async (data: BlockFormValues) => {
    const payload: CreateBarberBlockPayload = {
      startsAt: new Date(data.startsAt).toISOString(),
      endsAt: new Date(data.endsAt).toISOString(),
      description: data.description || undefined,
      isRecurring: data.isRecurring,
      recurrenceDays: data.isRecurring ? (data.selectedDays ?? null) : null,
      recurrenceEndsAt: data.isRecurring && data.recurrenceEndsAt
        ? new Date(data.recurrenceEndsAt).toISOString()
        : null,
    }
    await createBlock.mutateAsync(payload)
    reset()
    setOpen(false)
  }

  const formatDate = (iso: string) =>
    new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

  const bitsToLabels = (bits: number) =>
    DAYS.filter(d => (bits & d.bit) !== 0).map(d => d.label).join(', ')

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h2 className="text-lg font-semibold text-brand-white">Bloqueios</h2>
        <Button onClick={() => setOpen(true)}>Adicionar Bloqueio</Button>
      </div>

      {isLoading && <p className="text-brand-white/60">Carregando...</p>}

      {blocks?.length === 0 && (
        <p className="text-brand-white/60">Nenhum bloqueio cadastrado.</p>
      )}

      <ul className="space-y-2">
        {blocks?.map(block => (
          <li key={block.id} className="bg-brand-black-soft rounded-lg p-4 flex justify-between items-start">
            <div>
              <p className="text-brand-white font-medium">
                {formatDate(block.startsAt)} → {formatDate(block.endsAt)}
              </p>
              {block.description && (
                <p className="text-brand-white/70 text-sm">{block.description}</p>
              )}
              {block.isRecurring && block.recurrenceDays != null && (
                <p className="text-brand-gold text-xs mt-1">
                  Recorrente: {bitsToLabels(block.recurrenceDays)}
                  {block.recurrenceEndsAt && ` até ${formatDate(block.recurrenceEndsAt)}`}
                </p>
              )}
            </div>
            <button
              onClick={() => deleteBlock.mutate(block.id)}
              className="text-red-400 hover:text-red-300 text-sm ml-4"
            >
              Excluir
            </button>
          </li>
        ))}
      </ul>

      <Modal isOpen={open} onClose={() => { setOpen(false); reset() }} title="Adicionar Bloqueio">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Início</label>
            <Input type="datetime-local" {...register('startsAt')} />
            {errors.startsAt && <p className="text-red-400 text-xs">{errors.startsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Fim</label>
            <Input type="datetime-local" {...register('endsAt')} />
            {errors.endsAt && <p className="text-red-400 text-xs">{errors.endsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Descrição (opcional)</label>
            <Input type="text" placeholder="Ex: Almoço, Folga..." {...register('description')} />
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="isRecurring" {...register('isRecurring')} className="accent-brand-gold" />
            <label htmlFor="isRecurring" className="text-brand-white text-sm">Recorrente</label>
          </div>

          {isRecurring && (
            <>
              <div>
                <p className="text-brand-white/70 text-sm mb-2">Dias da semana</p>
                <div className="flex gap-2 flex-wrap">
                  {DAYS.map(d => (
                    <button
                      key={d.bit}
                      type="button"
                      onClick={() => toggleDay(d.bit)}
                      className={`px-3 py-1 rounded text-sm font-medium border transition-colors ${
                        (selectedDays & d.bit) !== 0
                          ? 'bg-brand-gold text-brand-black border-brand-gold'
                          : 'bg-transparent text-brand-white/70 border-brand-white/20'
                      }`}
                    >
                      {d.label}
                    </button>
                  ))}
                </div>
                {errors.selectedDays && <p className="text-red-400 text-xs mt-1">{errors.selectedDays.message}</p>}
              </div>
              <div>
                <label className="block text-brand-white/70 text-sm mb-1">Repetir até (opcional)</label>
                <Input type="date" {...register('recurrenceEndsAt')} />
              </div>
            </>
          )}

          <Button type="submit" disabled={createBlock.isPending} className="w-full">
            {createBlock.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </form>
      </Modal>
    </div>
  )
}
```

- [ ] **Step 6: Add Bloqueios tab to barber dashboard**

Open `frontend/src/app/barber/dashboard/page.tsx`. Find the existing tab state and add a "Bloqueios" tab alongside the existing tabs. Import `BlocksTab`:

```typescript
import BlocksTab from './BlocksTab'
```

Add `'bloqueios'` to the tab union type and render `<BlocksTab />` when that tab is active. The exact implementation depends on the existing tab structure — adapt to match it.

- [ ] **Step 7: Run tests**

```bash
cd frontend && npm test -- --reporter=verbose tests/unit/app/barber/BlocksTab.test.tsx
```
Expected: 3 passed

- [ ] **Step 8: Run full frontend tests**

```bash
cd frontend && npm test
```
Expected: all pass

- [ ] **Step 9: Commit**

```bash
git add frontend/src/lib/api/blocks.api.ts
git add frontend/src/hooks/useBarberBlocks.ts
git add frontend/src/app/barber/dashboard/BlocksTab.tsx
git add frontend/src/app/barber/dashboard/page.tsx
git add frontend/tests/unit/app/barber/BlocksTab.test.tsx
git commit -m "feat(frontend): barber blocks tab — list, add, delete"
```

---

### Task 7: Frontend — Admin Barbers Page — Blocks Section

**Files:**
- Create: `frontend/src/lib/api/admin-blocks.api.ts`
- Create: `frontend/src/hooks/useAdminBlocks.ts`
- Modify: `frontend/src/app/admin/barbers/page.tsx` — add blocks section per barber
- Create: `frontend/tests/unit/app/admin/AdminBlocksSection.test.tsx`

**Interfaces:**
- Consumes: `GET /api/v1/admin/barbers/{barberId}/blocks`, `POST /api/v1/admin/barbers/{barberId}/blocks`, `DELETE /api/v1/admin/barbers/{barberId}/blocks/{id}`
- Produces: admin UI to manage any barber's blocks

- [ ] **Step 1: Create admin blocks API client**

```typescript
// frontend/src/lib/api/admin-blocks.api.ts
import { apiClient } from '@/lib/api/client'
import type { BarberBlockDto, CreateBarberBlockPayload } from './blocks.api'

export const adminBlocksApi = {
  getBlocks: (barberId: string): Promise<BarberBlockDto[]> =>
    apiClient.get(`/admin/barbers/${barberId}/blocks`).then(r => r.data),

  createBlock: (barberId: string, payload: CreateBarberBlockPayload): Promise<{ id: string }> =>
    apiClient.post(`/admin/barbers/${barberId}/blocks`, payload).then(r => r.data),

  deleteBlock: (barberId: string, blockId: string): Promise<void> =>
    apiClient.delete(`/admin/barbers/${barberId}/blocks/${blockId}`).then(() => undefined),
}
```

- [ ] **Step 2: Create admin blocks hooks**

```typescript
// frontend/src/hooks/useAdminBlocks.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminBlocksApi } from '@/lib/api/admin-blocks.api'
import type { CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const blocksKey = (barberId: string) => ['admin', 'barbers', barberId, 'blocks'] as const

export function useAdminBarberBlocks(barberId: string) {
  return useQuery({
    queryKey: blocksKey(barberId),
    queryFn: () => adminBlocksApi.getBlocks(barberId),
  })
}

export function useAdminCreateBarberBlock(barberId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBarberBlockPayload) => adminBlocksApi.createBlock(barberId, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(barberId) }),
  })
}

export function useAdminDeleteBarberBlock(barberId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (blockId: string) => adminBlocksApi.deleteBlock(barberId, blockId),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(barberId) }),
  })
}
```

- [ ] **Step 3: Write failing test**

```typescript
// frontend/tests/unit/app/admin/AdminBlocksSection.test.tsx
import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'
import AdminBlocksSection from '@/app/admin/barbers/AdminBlocksSection'

const BARBER_ID = 'barber-abc'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

beforeEach(() => {
  server.use(
    http.get(`*/admin/barbers/${BARBER_ID}/blocks`, () =>
      HttpResponse.json([
        {
          id: 'blk-1',
          startsAt: '2026-08-05T12:00:00Z',
          endsAt: '2026-08-05T13:00:00Z',
          description: 'Folga',
          isRecurring: false,
          recurrenceDays: null,
          recurrenceEndsAt: null,
          createdAt: '2026-07-01T00:00:00Z',
        },
      ])
    )
  )
})

describe('AdminBlocksSection', () => {
  it('renders block list for barber', async () => {
    render(<AdminBlocksSection barberId={BARBER_ID} />, { wrapper })
    expect(await screen.findByText('Folga')).toBeInTheDocument()
  })

  it('renders add block button', () => {
    render(<AdminBlocksSection barberId={BARBER_ID} />, { wrapper })
    expect(screen.getByRole('button', { name: /adicionar bloqueio/i })).toBeInTheDocument()
  })
})
```

- [ ] **Step 4: Run test to verify it fails**

```bash
cd frontend && npm test -- --reporter=verbose tests/unit/app/admin/AdminBlocksSection.test.tsx
```
Expected: FAIL — module not found

- [ ] **Step 5: Create AdminBlocksSection component**

```tsx
// frontend/src/app/admin/barbers/AdminBlocksSection.tsx
'use client'

import { useState } from 'react'
import { useAdminBarberBlocks, useAdminCreateBarberBlock, useAdminDeleteBarberBlock } from '@/hooks/useAdminBlocks'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const DAYS = [
  { label: 'Dom', bit: 1 }, { label: 'Seg', bit: 2 }, { label: 'Ter', bit: 4 },
  { label: 'Qua', bit: 8 }, { label: 'Qui', bit: 16 }, { label: 'Sex', bit: 32 }, { label: 'Sáb', bit: 64 },
]

const blockSchema = z.object({
  startsAt: z.string().min(1),
  endsAt: z.string().min(1),
  description: z.string().max(200).optional(),
  isRecurring: z.boolean(),
  selectedDays: z.number().min(0).max(127).optional(),
  recurrenceEndsAt: z.string().optional(),
}).refine(d => d.endsAt > d.startsAt, { message: 'Fim deve ser após início', path: ['endsAt'] })
 .refine(d => !d.isRecurring || (d.selectedDays && d.selectedDays > 0), { message: 'Selecione ao menos um dia', path: ['selectedDays'] })

type FormValues = z.infer<typeof blockSchema>

export default function AdminBlocksSection({ barberId }: { barberId: string }) {
  const { data: blocks, isLoading } = useAdminBarberBlocks(barberId)
  const createBlock = useAdminCreateBarberBlock(barberId)
  const deleteBlock = useAdminDeleteBarberBlock(barberId)
  const [open, setOpen] = useState(false)

  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } =
    useForm<FormValues>({ resolver: zodResolver(blockSchema), defaultValues: { isRecurring: false, selectedDays: 0 } })

  const isRecurring = watch('isRecurring')
  const selectedDays = watch('selectedDays') ?? 0

  const onSubmit = async (data: FormValues) => {
    const payload: CreateBarberBlockPayload = {
      startsAt: new Date(data.startsAt).toISOString(),
      endsAt: new Date(data.endsAt).toISOString(),
      description: data.description || undefined,
      isRecurring: data.isRecurring,
      recurrenceDays: data.isRecurring ? (data.selectedDays ?? null) : null,
      recurrenceEndsAt: data.isRecurring && data.recurrenceEndsAt ? new Date(data.recurrenceEndsAt).toISOString() : null,
    }
    await createBlock.mutateAsync(payload)
    reset()
    setOpen(false)
  }

  const formatDate = (iso: string) => new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
  const bitsToLabels = (bits: number) => DAYS.filter(d => (bits & d.bit) !== 0).map(d => d.label).join(', ')

  return (
    <div className="space-y-3 mt-4">
      <div className="flex justify-between items-center">
        <h3 className="text-brand-white font-medium">Bloqueios</h3>
        <Button onClick={() => setOpen(true)}>Adicionar Bloqueio</Button>
      </div>

      {isLoading && <p className="text-brand-white/60 text-sm">Carregando...</p>}
      {blocks?.length === 0 && <p className="text-brand-white/60 text-sm">Nenhum bloqueio.</p>}

      <ul className="space-y-2">
        {blocks?.map(block => (
          <li key={block.id} className="bg-brand-black rounded p-3 flex justify-between items-start">
            <div>
              <p className="text-brand-white text-sm">{formatDate(block.startsAt)} → {formatDate(block.endsAt)}</p>
              {block.description && <p className="text-brand-white/60 text-xs">{block.description}</p>}
              {block.isRecurring && block.recurrenceDays != null && (
                <p className="text-brand-gold text-xs">{bitsToLabels(block.recurrenceDays)}</p>
              )}
            </div>
            <button onClick={() => deleteBlock.mutate(block.id)} className="text-red-400 text-xs ml-2">Excluir</button>
          </li>
        ))}
      </ul>

      <Modal isOpen={open} onClose={() => { setOpen(false); reset() }} title="Adicionar Bloqueio">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Início</label>
            <Input type="datetime-local" {...register('startsAt')} />
            {errors.startsAt && <p className="text-red-400 text-xs">{errors.startsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Fim</label>
            <Input type="datetime-local" {...register('endsAt')} />
            {errors.endsAt && <p className="text-red-400 text-xs">{errors.endsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Descrição</label>
            <Input type="text" {...register('description')} />
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="rec" {...register('isRecurring')} className="accent-brand-gold" />
            <label htmlFor="rec" className="text-brand-white text-sm">Recorrente</label>
          </div>
          {isRecurring && (
            <>
              <div className="flex gap-2 flex-wrap">
                {DAYS.map(d => (
                  <button key={d.bit} type="button"
                    onClick={() => setValue('selectedDays', selectedDays ^ d.bit)}
                    className={`px-2 py-1 rounded text-xs border ${(selectedDays & d.bit) !== 0 ? 'bg-brand-gold text-brand-black border-brand-gold' : 'text-brand-white/70 border-brand-white/20'}`}>
                    {d.label}
                  </button>
                ))}
              </div>
              <Input type="date" {...register('recurrenceEndsAt')} placeholder="Repetir até (opcional)" />
            </>
          )}
          <Button type="submit" disabled={createBlock.isPending} className="w-full">
            {createBlock.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </form>
      </Modal>
    </div>
  )
}
```

- [ ] **Step 6: Add AdminBlocksSection to admin/barbers/page.tsx**

Open `frontend/src/app/admin/barbers/page.tsx`. Find where individual barber cards/rows are rendered. Import and add `<AdminBlocksSection barberId={barber.id} />` inside each barber's expandable section or below each barber card.

Add import:
```typescript
import AdminBlocksSection from './AdminBlocksSection'
```

- [ ] **Step 7: Run tests**

```bash
cd frontend && npm test -- --reporter=verbose tests/unit/app/admin/AdminBlocksSection.test.tsx
```
Expected: 2 passed

- [ ] **Step 8: Run full test suite**

```bash
cd frontend && npm test
```
Expected: all pass

- [ ] **Step 9: Commit**

```bash
git add frontend/src/lib/api/admin-blocks.api.ts
git add frontend/src/hooks/useAdminBlocks.ts
git add frontend/src/app/admin/barbers/AdminBlocksSection.tsx
git add frontend/src/app/admin/barbers/page.tsx
git add frontend/tests/unit/app/admin/AdminBlocksSection.test.tsx
git commit -m "feat(frontend): admin blocks section per barber"
```
