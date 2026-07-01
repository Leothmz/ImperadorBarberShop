# Admin Roles, Service Add-ons & Financial Dashboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce the `Admin` role with protected management area (barber creation, service/add-on CRUD, financial dashboard) and expose service add-ons to the public booking wizard.

**Architecture:** New `Admin = 2` enum value gates a protected `/admin/*` area. Services self-reference via a `ServiceAddon` M:N junction. Photos go to Cloudinary; the URL is stored in `Service.PhotoUrl` and `Barber.PhotoUrl`. Financial data aggregates from completed `Appointment` + `AppointmentService` rows.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, CloudinaryDotNet SDK, Next.js 15, TanStack Query v5, React Hook Form + Zod, Tailwind CSS v4.

## Global Constraints

- All UI text in Brazilian Portuguese
- Currency: R$ X,XX — dates: DD/MM/YYYY
- Brand gold `#C9A84C`, black `#0D0D0D`, black-soft `#1A1A1A`, white `#F5F5F5`
- Fonts: Montserrat (headings), Inter (body)
- Admin credentials **never** logged or hardcoded — env vars only
- Image upload: max 5 MB, `image/jpeg` or `image/png`
- All financial filters on `Status = Completed` appointments only
- Follow existing patterns: co-located command/query/handler/validator in one `.cs` file; NSubstitute + FluentAssertions in unit tests; Testcontainers in integration tests

---

## Task 1: Domain model changes + EF migration

**Files:**
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Enums/UserRole.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/User.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Service.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Barber.cs`
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/ServiceAddon.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IServiceRepository.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IBarberRepository.cs`
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IServiceAddonRepository.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppointmentRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ServiceConfiguration.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/BarberConfiguration.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ServiceAddonConfiguration.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/ServiceAddonRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/ServiceRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/BarberRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppointmentRepository.cs`
- Test: `backend/tests/ImperadorBarberShop.UnitTests/Domain/DomainEntityTests.cs`

**Interfaces:**
- Produces: `UserRole.Admin`, `User.CreateAdmin()`, `User.UpdatePasswordHash()`, `Service.Update()`, `Service.UpdatePhoto()`, `Barber.IsActive`, `Barber.Deactivate()`, `Barber.Activate()`, `Barber.UpdatePhoto()`, `ServiceAddon`, `IServiceAddonRepository`, extended `IServiceRepository`, extended `IBarberRepository`, extended `IAppointmentRepository`

- [ ] **Step 1: Write failing domain tests**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Domain/DomainEntityTests.cs
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.UnitTests.Domain;

public class DomainEntityTests
{
    [Fact]
    public void UserRole_Admin_HasValue2()
        => ((int)UserRole.Admin).Should().Be(2);

    [Fact]
    public void User_CreateAdmin_SetsRoleAdmin()
    {
        var user = User.CreateAdmin("Administrador", "admin@test.com", "hash");
        user.Role.Should().Be(UserRole.Admin);
        user.Name.Should().Be("Administrador");
    }

    [Fact]
    public void Barber_NewBarber_IsActiveByDefault()
    {
        var barber = Barber.Create(Guid.NewGuid());
        barber.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Barber_Deactivate_SetsIsActiveFalse()
    {
        var barber = Barber.Create(Guid.NewGuid());
        barber.Deactivate();
        barber.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ServiceAddon_Create_SameId_Throws()
    {
        var id = Guid.NewGuid();
        var act = () => ServiceAddon.Create(id, id);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ServiceAddon_Create_DifferentIds_Succeeds()
    {
        var addon = ServiceAddon.Create(Guid.NewGuid(), Guid.NewGuid());
        addon.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run — expect FAIL (types don't exist yet)**

```
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "DomainEntityTests" 2>&1 | tail -5
```

- [ ] **Step 3: Update `UserRole.cs`**

```csharp
namespace ImperadorBarberShop.Domain.Enums;

public enum UserRole
{
    Client = 0,
    Barber = 1,
    Admin = 2
}
```

- [ ] **Step 4: Update `User.cs` — add `CreateAdmin` + `UpdatePasswordHash`**

Add after `CreateBarber`:
```csharp
public static User CreateAdmin(string name, string email, string passwordHash)
{
    return new User
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = email,
        PasswordHash = passwordHash,
        Role = UserRole.Admin,
        CreatedAt = DateTime.UtcNow
    };
}

public void UpdatePasswordHash(string newHash) => PasswordHash = newHash;
```

- [ ] **Step 5: Update `Service.cs` — add `PhotoUrl`, `Update`, `UpdatePhoto`**

Add fields and methods:
```csharp
public string? PhotoUrl { get; private set; }

public void Update(string name, string description, int durationMinutes, decimal price)
{
    Name = name;
    Description = description;
    DurationMinutes = durationMinutes;
    Price = price;
}

public void UpdatePhoto(string photoUrl) => PhotoUrl = photoUrl;
```

- [ ] **Step 6: Update `Barber.cs` — add `IsActive`, `PhotoUrl`, `Deactivate`, `Activate`, `UpdatePhoto`**

```csharp
public bool IsActive { get; private set; }
public string? PhotoUrl { get; private set; }
```

In `Create` factory: add `IsActive = true`.

Add methods:
```csharp
public void Deactivate() => IsActive = false;
public void Activate() => IsActive = true;
public void UpdatePhoto(string photoUrl) => PhotoUrl = photoUrl;
```

- [ ] **Step 7: Create `ServiceAddon.cs`**

```csharp
namespace ImperadorBarberShop.Domain.Entities;

public class ServiceAddon
{
    public Guid ParentServiceId { get; private set; }
    public Guid AddonServiceId { get; private set; }
    public Service AddonService { get; private set; } = null!;

    private ServiceAddon() { }

    public static ServiceAddon Create(Guid parentServiceId, Guid addonServiceId)
    {
        if (parentServiceId == addonServiceId)
            throw new ArgumentException("A service cannot be its own add-on.");
        return new ServiceAddon { ParentServiceId = parentServiceId, AddonServiceId = addonServiceId };
    }
}
```

- [ ] **Step 8: Create `IServiceAddonRepository.cs`**

```csharp
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IServiceAddonRepository
{
    Task<List<ServiceAddon>> GetByParentIdsAsync(IEnumerable<Guid> parentIds, CancellationToken ct = default);
    Task<ServiceAddon?> GetAsync(Guid parentId, Guid addonId, CancellationToken ct = default);
    Task AddAsync(ServiceAddon addon, CancellationToken ct = default);
    void Remove(ServiceAddon addon);
}
```

- [ ] **Step 9: Extend `IServiceRepository.cs`**

Add to interface:
```csharp
Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default);
Task AddAsync(Service service, CancellationToken cancellationToken = default);
Task UpdateAsync(Service service, CancellationToken cancellationToken = default);
```

- [ ] **Step 10: Extend `IBarberRepository.cs`**

Add to interface:
```csharp
Task<List<Barber>> GetAllActiveAsync(CancellationToken cancellationToken = default);
```

- [ ] **Step 11: Extend `IAppointmentRepository.cs`**

Add to interface:
```csharp
Task<List<Appointment>> GetCompletedByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
```

- [ ] **Step 12: Update EF configurations**

**`ServiceConfiguration.cs`** — add inside `Configure`:
```csharp
builder.Property(s => s.PhotoUrl).HasMaxLength(500);
```
The seed `HasData` call uses `Service.CreateWithId` which doesn't set `PhotoUrl` — nullable, no change needed.

**`BarberConfiguration.cs`** — add inside `Configure`:
```csharp
builder.Property(b => b.IsActive).IsRequired().HasDefaultValue(true);
builder.Property(b => b.PhotoUrl).HasMaxLength(500);
```

**Create `ServiceAddonConfiguration.cs`:**
```csharp
using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class ServiceAddonConfiguration : IEntityTypeConfiguration<ServiceAddon>
{
    public void Configure(EntityTypeBuilder<ServiceAddon> builder)
    {
        builder.HasKey(a => new { a.ParentServiceId, a.AddonServiceId });

        builder.HasOne(a => a.AddonService)
            .WithMany()
            .HasForeignKey(a => a.AddonServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(a => a.ParentServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ServiceAddons_NoCycles",
            "\"ParentServiceId\" <> \"AddonServiceId\""));
    }
}
```

- [ ] **Step 13: Update `AppDbContext.cs`** — add DbSet:

```csharp
public DbSet<ServiceAddon> ServiceAddons => Set<ServiceAddon>();
```

- [ ] **Step 14: Implement new repository methods**

**`ServiceAddonRepository.cs`** (new file):
```csharp
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ServiceAddonRepository : IServiceAddonRepository
{
    private readonly AppDbContext _context;
    public ServiceAddonRepository(AppDbContext context) => _context = context;

    public async Task<List<ServiceAddon>> GetByParentIdsAsync(IEnumerable<Guid> parentIds, CancellationToken ct = default)
        => await _context.ServiceAddons
            .Include(a => a.AddonService)
            .Where(a => parentIds.Contains(a.ParentServiceId))
            .ToListAsync(ct);

    public async Task<ServiceAddon?> GetAsync(Guid parentId, Guid addonId, CancellationToken ct = default)
        => await _context.ServiceAddons
            .FirstOrDefaultAsync(a => a.ParentServiceId == parentId && a.AddonServiceId == addonId, ct);

    public async Task AddAsync(ServiceAddon addon, CancellationToken ct = default)
        => await _context.ServiceAddons.AddAsync(addon, ct);

    public void Remove(ServiceAddon addon) => _context.ServiceAddons.Remove(addon);
}
```

**`ServiceRepository.cs`** — add missing methods:
```csharp
public async Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default)
    => await _context.Services.ToListAsync(cancellationToken);

public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
    => await _context.Services.AddAsync(service, cancellationToken);

public Task UpdateAsync(Service service, CancellationToken cancellationToken = default)
{
    _context.Services.Update(service);
    return Task.CompletedTask;
}
```

**`BarberRepository.cs`** — add `GetAllActiveAsync`:
```csharp
public async Task<List<Barber>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    => await _context.Barbers
        .Include(b => b.User)
        .Include(b => b.Availability)
        .Where(b => b.IsActive)
        .ToListAsync(cancellationToken);
```

**`AppointmentRepository.cs`** — add financial method:
```csharp
public async Task<List<Appointment>> GetCompletedByDateRangeAsync(
    DateTime from, DateTime to, CancellationToken cancellationToken = default)
    => await _context.Appointments
        .Include(a => a.Barber).ThenInclude(b => b.User)
        .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
        .Where(a => a.Status == AppointmentStatus.Completed
            && a.ScheduledAt >= from
            && a.ScheduledAt <= to)
        .OrderBy(a => a.ScheduledAt)
        .ToListAsync(cancellationToken);
```

- [ ] **Step 15: Update `GetBarbersQuery` handler to use `GetAllActiveAsync`**

In `GetBarbersQuery.cs`, change the handler to call `_barberRepository.GetAllActiveAsync(cancellationToken)` instead of `GetAllAsync`.

- [ ] **Step 16: Register `IServiceAddonRepository` in DI**

In `DependencyInjection.cs`, add:
```csharp
services.AddScoped<IServiceAddonRepository, ServiceAddonRepository>();
```

- [ ] **Step 17: Generate and apply EF migration**

```bash
cd backend
dotnet ef migrations add AddAdminAndServiceAddons \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
dotnet ef database update \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```

- [ ] **Step 18: Run domain tests — expect PASS**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "DomainEntityTests"
```
Expected: all 6 tests PASS.

- [ ] **Step 19: Run full test suite — no regressions**

```bash
cd backend && dotnet test
```

- [ ] **Step 20: Commit**

```bash
git add backend/src/Domain backend/src/Infrastructure backend/tests/ImperadorBarberShop.UnitTests/Domain
git commit -m "feat(domain): add Admin role, ServiceAddon entity, IsActive/PhotoUrl on Barber and Service"
```

---

## Task 2: Cloudinary integration

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/Interfaces/IImageService.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Settings/CloudinarySettings.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/CloudinaryImageService.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`
- Modify: `backend/tests/ImperadorBarberShop.IntegrationTests/WebAppFixture.cs`

**Interfaces:**
- Produces: `IImageService.UploadAsync(Stream, string, string) → Task<string>`

- [ ] **Step 1: Create `IImageService.cs`**

```csharp
namespace ImperadorBarberShop.Application.Interfaces;

public interface IImageService
{
    /// <summary>Uploads image and returns the public URL.</summary>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create `CloudinarySettings.cs`**

```csharp
namespace ImperadorBarberShop.Infrastructure.Settings;

public class CloudinarySettings
{
    public const string SectionName = "Cloudinary";
    public string CloudName { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
}
```

- [ ] **Step 3: Add CloudinaryDotNet NuGet to Infrastructure project**

```bash
cd backend
dotnet add src/Infrastructure/ImperadorBarberShop.Infrastructure/ImperadorBarberShop.Infrastructure.csproj \
  package CloudinaryDotNet
```

- [ ] **Step 4: Create `CloudinaryImageService.cs`**

```csharp
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace ImperadorBarberShop.Infrastructure.Services;

public class CloudinaryImageService : IImageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageService(IOptions<CloudinarySettings> settings)
    {
        var s = settings.Value;
        var account = new Account(s.CloudName, s.ApiKey, s.ApiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = "imperador-barber",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }
}
```

- [ ] **Step 5: Update `DependencyInjection.cs` — register Cloudinary**

```csharp
services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
services.AddScoped<IImageService, CloudinaryImageService>();
```

- [ ] **Step 6: Add Cloudinary placeholder to `appsettings.Development.json` (gitignored)**

```json
"Cloudinary": {
  "CloudName": "<your-cloud-name>",
  "ApiKey": "<your-api-key>",
  "ApiSecret": "<your-api-secret>"
}
```

- [ ] **Step 7: Update `WebAppFixture.cs` — register fake image service for tests**

Add to `ConfigureWebHost` → `builder.ConfigureServices`:
```csharp
// Replace real Cloudinary with a no-op fake for integration tests
services.AddScoped<IImageService, FakeImageService>();
```

Add inner class to WebAppFixture file (outside the fixture class):
```csharp
public class FakeImageService : IImageService
{
    public Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
        => Task.FromResult($"https://fake-cloudinary.com/{Guid.NewGuid()}/{fileName}");
}
```

Also add `Admin__Email` and `Admin__Password` to `TestEnvironmentVariables`:
```csharp
["Admin__Email"] = "admin@test.com",
["Admin__Password"] = "AdminTest123!",
```

- [ ] **Step 8: Build — no errors**

```bash
cd backend && dotnet build
```

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(infra): Cloudinary image upload service + IImageService interface"
```

---

## Task 3: Admin seed + authorization

**Files:**
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/AdminSeedService.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Program.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AuthController.cs`
- Test: `backend/tests/ImperadorBarberShop.UnitTests/Admin/AdminSeedServiceTests.cs`
- Test: `backend/tests/ImperadorBarberShop.IntegrationTests/Admin/AdminAuthTests.cs`

**Interfaces:**
- Consumes: `User.CreateAdmin()`, `IUserRepository`, `IPasswordHasher`, `IUnitOfWork`
- Produces: `AdminSeedService.SeedAsync()`, `RequireAdminRole` policy in Program.cs

- [ ] **Step 1: Write failing unit test**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Admin/AdminSeedServiceTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Admin;

public class AdminSeedServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private AdminSeedService BuildService(string? email = "admin@test.com", string? password = "pass123")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = email,
                ["Admin:Password"] = password
            })
            .Build();
        return new AdminSeedService(_userRepo, _hasher, _uow, config);
    }

    [Fact]
    public async Task SeedAsync_NoAdminExists_CreatesAdmin()
    {
        _userRepo.GetAdminAsync(Arg.Any<CancellationToken>()).Returns((User?)null);
        _hasher.Hash("pass123").Returns("hashed");

        await BuildService().SeedAsync(CancellationToken.None);

        await _userRepo.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == UserRole.Admin && u.Email == "admin@test.com"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_AdminAlreadyExists_DoesNothing()
    {
        var existing = User.CreateAdmin("Admin", "admin@test.com", "hash");
        _userRepo.GetAdminAsync(Arg.Any<CancellationToken>()).Returns(existing);

        await BuildService().SeedAsync(CancellationToken.None);

        await _userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_MissingEnvVar_Throws()
    {
        _userRepo.GetAdminAsync(Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => BuildService(email: null).SeedAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ADMIN__EMAIL*");
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "AdminSeedServiceTests" 2>&1 | tail -5
```

- [ ] **Step 3: Add `GetAdminAsync` to `IUserRepository`**

```csharp
Task<User?> GetAdminAsync(CancellationToken cancellationToken = default);
```

Implement in `UserRepository.cs`:
```csharp
public async Task<User?> GetAdminAsync(CancellationToken cancellationToken = default)
    => await _context.Users
        .FirstOrDefaultAsync(u => u.Role == UserRole.Admin, cancellationToken);
```

- [ ] **Step 4: Create `AdminSeedService.cs`**

```csharp
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ImperadorBarberShop.Infrastructure.Services;

public class AdminSeedService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AdminSeedService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _userRepository.GetAdminAsync(cancellationToken);
        if (existing is not null) return;

        var email = _configuration["Admin:Email"];
        var password = _configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Admin credentials not configured. Set ADMIN__EMAIL environment variable.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Admin credentials not configured. Set ADMIN__PASSWORD environment variable.");

        var hash = _passwordHasher.Hash(password);
        var admin = User.CreateAdmin("Administrador", email, hash);

        await _userRepository.AddAsync(admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run seed tests — expect PASS**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "AdminSeedServiceTests"
```

- [ ] **Step 6: Update `Program.cs` — add Admin policy + call seed**

In `AddAuthorization`:
```csharp
options.AddPolicy("RequireAdminRole", policy => policy.RequireClaim("role", "Admin"));
```

After `await db.Database.MigrateAsync();` (inside the dev block), and also in production block, add:
```csharp
using var seedScope = app.Services.CreateScope();
var seedService = seedScope.ServiceProvider.GetRequiredService<AdminSeedService>();
await seedService.SeedAsync();
```

Move `MigrateAsync` + seed out of the `if (IsDevelopment)` block so it runs in all environments:
```csharp
// Auto-migrate and seed on startup (all environments)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "O Imperador API v1"));
}
```

Register `AdminSeedService` in `DependencyInjection.cs`:
```csharp
services.AddScoped<AdminSeedService>();
```

- [ ] **Step 7: Remove `POST /auth/register/barber` from `AuthController.cs`**

Delete the `RegisterBarber` action method and its `[HttpPost("register/barber")]` endpoint entirely. The `RegisterBarberCommand` class itself stays (used in unit tests as a test double pattern).

- [ ] **Step 8: Build and run all tests**

```bash
cd backend && dotnet build && dotnet test
```

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(api): admin seed service, Admin auth policy, remove public barber registration"
```

---

## Task 4: Admin barber management (backend)

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/DTOs/AdminBarberDto.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/CreateBarberByAdminCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/DeactivateBarberCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/ActivateBarberCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/ChangeAdminPasswordCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetAdminBarbersQuery.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Mappings/MappingProfile.cs`
- Create: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs`
- Test: `backend/tests/ImperadorBarberShop.UnitTests/Admin/CreateBarberByAdminCommandHandlerTests.cs`
- Test: `backend/tests/ImperadorBarberShop.IntegrationTests/Admin/AdminBarbersControllerTests.cs`

**Interfaces:**
- Consumes: `IImageService.UploadAsync`, `User.CreateAdmin`, `Barber.Deactivate/Activate`, `IBarberRepository.GetAllAsync` (admin, all barbers)
- Produces: `POST /admin/barbers`, `GET /admin/barbers`, `PATCH /admin/barbers/{id}/deactivate`, `PATCH /admin/barbers/{id}/activate`, `PATCH /admin/profile/password`

- [ ] **Step 1: Write failing unit test**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Admin/CreateBarberByAdminCommandHandlerTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Admin;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Admin;

public class CreateBarberByAdminCommandHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IBarberRepository _barberRepo = Substitute.For<IBarberRepository>();
    private readonly IBarberAvailabilityRepository _availRepo = Substitute.For<IBarberAvailabilityRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateBarberByAdminCommandHandler _handler;

    public CreateBarberByAdminCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
        _handler = new CreateBarberByAdminCommandHandler(
            _userRepo, _barberRepo, _availRepo, _hasher, _uow);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesBarber()
    {
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new CreateBarberByAdminCommand(
            "João Barbeiro", "joao@test.com", "senha123",
            new List<AvailabilitySlotInput>(), PhotoUrl: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _barberRepo.Received(1).AddAsync(Arg.Any<Barber>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_Throws()
    {
        var existing = User.CreateBarber("Existing", "joao@test.com", "hash");
        _userRepo.GetByEmailAsync("joao@test.com", Arg.Any<CancellationToken>()).Returns(existing);

        var command = new CreateBarberByAdminCommand(
            "João", "joao@test.com", "senha123", new List<AvailabilitySlotInput>(), null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already registered*");
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "CreateBarberByAdminCommandHandlerTests" 2>&1 | tail -5
```

- [ ] **Step 3: Create `AdminBarberDto.cs`**

```csharp
namespace ImperadorBarberShop.Application.DTOs;

public record AdminBarberDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public decimal AverageRating { get; init; }
    public bool IsActive { get; init; }
    public List<BarberAvailabilityDto> Availability { get; init; } = [];
}
```

- [ ] **Step 4: Create `CreateBarberByAdminCommand.cs`**

```csharp
using FluentValidation;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record CreateBarberByAdminCommand(
    string Name,
    string Email,
    string Password,
    List<AvailabilitySlotInput> Availability,
    string? PhotoUrl) : IRequest<Guid>;

public class CreateBarberByAdminCommandValidator : AbstractValidator<CreateBarberByAdminCommand>
{
    public CreateBarberByAdminCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleForEach(x => x.Availability).ChildRules(slot =>
            slot.RuleFor(s => s.EndTime).GreaterThan(s => s.StartTime)
                .WithMessage("EndTime must be after StartTime."));
    }
}

public class CreateBarberByAdminCommandHandler : IRequestHandler<CreateBarberByAdminCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBarberByAdminCommandHandler(
        IUserRepository userRepository,
        IBarberRepository barberRepository,
        IBarberAvailabilityRepository availabilityRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _barberRepository = barberRepository;
        _availabilityRepository = availabilityRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateBarberByAdminCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");

        var hash = _passwordHasher.Hash(request.Password);
        var user = User.CreateBarber(request.Name, request.Email, hash);
        await _userRepository.AddAsync(user, cancellationToken);

        var barber = Barber.Create(user.Id);
        if (request.PhotoUrl is not null)
            barber.UpdatePhoto(request.PhotoUrl);
        await _barberRepository.AddAsync(barber, cancellationToken);

        var availabilities = request.Availability
            .Select(a => BarberAvailability.Create(barber.Id, a.DayOfWeek, a.StartTime, a.EndTime))
            .ToList();
        if (availabilities.Count > 0)
            await _availabilityRepository.AddRangeAsync(availabilities, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
```

- [ ] **Step 5: Create `DeactivateBarberCommand.cs`**

```csharp
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record DeactivateBarberCommand(Guid BarberId) : IRequest;

public class DeactivateBarberCommandHandler : IRequestHandler<DeactivateBarberCommand>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateBarberCommandHandler(IBarberRepository barberRepository, IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateBarberCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken)
            ?? throw new KeyNotFoundException($"Barber {request.BarberId} not found.");
        barber.Deactivate();
        await _barberRepository.UpdateAsync(barber, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 6: Create `ActivateBarberCommand.cs`** — same pattern as Deactivate, calls `barber.Activate()`.

- [ ] **Step 7: Create `ChangeAdminPasswordCommand.cs`**

```csharp
using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record ChangeAdminPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest;

public class ChangeAdminPasswordCommandValidator : AbstractValidator<ChangeAdminPasswordCommand>
{
    public ChangeAdminPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(100);
    }
}

public class ChangeAdminPasswordCommandHandler : IRequestHandler<ChangeAdminPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeAdminPasswordCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ChangeAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var newHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatePasswordHash(newHash);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

Add `GetByIdAsync` to `IUserRepository` if not present, and implement in `UserRepository`:
```csharp
Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
// impl: await _context.Users.FindAsync(new object[] { id }, cancellationToken);
```

- [ ] **Step 8: Create `GetAdminBarbersQuery.cs`**

```csharp
using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetAdminBarbersQuery : IRequest<List<AdminBarberDto>>;

public class GetAdminBarbersQueryHandler : IRequestHandler<GetAdminBarbersQuery, List<AdminBarberDto>>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IMapper _mapper;

    public GetAdminBarbersQueryHandler(IBarberRepository barberRepository, IMapper mapper)
    {
        _barberRepository = barberRepository;
        _mapper = mapper;
    }

    public async Task<List<AdminBarberDto>> Handle(GetAdminBarbersQuery request, CancellationToken cancellationToken)
    {
        var barbers = await _barberRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<AdminBarberDto>>(barbers);
    }
}
```

- [ ] **Step 9: Update `MappingProfile.cs` — add AdminBarberDto mapping**

```csharp
CreateMap<Barber, AdminBarberDto>()
    .ForMember(d => d.Name, o => o.MapFrom(s => s.User.Name))
    .ForMember(d => d.Email, o => o.MapFrom(s => s.User.Email))
    .ForMember(d => d.Availability, o => o.MapFrom(s => s.Availability));
```

Also update existing `BarberDto` mapping to include `PhotoUrl` and `IsActive`:
```csharp
CreateMap<Barber, BarberDto>()
    .ForMember(d => d.Name, o => o.MapFrom(s => s.User.Name))
    .ForMember(d => d.Email, o => o.MapFrom(s => s.User.Email))
    .ForMember(d => d.Availability, o => o.MapFrom(s => s.Availability));
// PhotoUrl and IsActive map automatically by name convention
```

Update `BarberDto` to include the new fields:
```csharp
public string? PhotoUrl { get; init; }
public bool IsActive { get; init; }
```

- [ ] **Step 10: Create `AdminController.cs`**

```csharp
using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Admin;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Application.Queries.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IImageService _imageService;

    public AdminController(IMediator mediator, IImageService imageService)
    {
        _mediator = mediator;
        _imageService = imageService;
    }

    [HttpGet("barbers")]
    public async Task<IActionResult> GetBarbers(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAdminBarbersQuery(), ct));

    [HttpPost("barbers")]
    public async Task<IActionResult> CreateBarber(
        [FromForm] CreateBarberRequest request,
        CancellationToken ct)
    {
        string? photoUrl = null;
        if (request.Photo is not null)
        {
            ValidateImage(request.Photo);
            photoUrl = await _imageService.UploadAsync(
                request.Photo.OpenReadStream(), request.Photo.FileName,
                request.Photo.ContentType, ct);
        }

        var availability = request.Availability ?? new List<AvailabilitySlotInput>();
        var id = await _mediator.Send(
            new CreateBarberByAdminCommand(request.Name, request.Email, request.Password, availability, photoUrl), ct);

        return CreatedAtAction(nameof(GetBarbers), new { id }, new { id });
    }

    [HttpPatch("barbers/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateBarber(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeactivateBarberCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("barbers/{id:guid}/activate")]
    public async Task<IActionResult> ActivateBarber(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ActivateBarberCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("profile/password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
        await _mediator.Send(new ChangeAdminPasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }

    private static void ValidateImage(IFormFile file)
    {
        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("Image must be smaller than 5 MB.");
        if (file.ContentType is not ("image/jpeg" or "image/png"))
            throw new ArgumentException("Only JPEG and PNG images are accepted.");
    }
}

public record CreateBarberRequest(
    string Name,
    string Email,
    string Password,
    IFormFile? Photo,
    List<AvailabilitySlotInput>? Availability);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
```

- [ ] **Step 11: Run unit tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "CreateBarberByAdminCommandHandlerTests"
```
Expected: PASS.

- [ ] **Step 12: Run all tests**

```bash
cd backend && dotnet test
```

- [ ] **Step 13: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(api): admin barber management — create, deactivate, activate, change password"
```

---

## Task 5: Service CRUD + add-ons (backend)

**Files:**
- Modify: `backend/src/Application/ImperadorBarberShop.Application/DTOs/ServiceDto.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Services/CreateServiceCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Services/UpdateServiceCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Services/DeactivateServiceCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Services/ActivateServiceCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Services/AddServiceAddonCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Services/RemoveServiceAddonCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Queries/Services/GetServicesQuery.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Mappings/MappingProfile.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/ServicesController.cs`
- Test: `backend/tests/ImperadorBarberShop.UnitTests/Services/ServiceAddonCommandHandlerTests.cs`

**Interfaces:**
- Produces: `ServiceDto` with `PhotoUrl` + `Addons`, admin service endpoints in `ServicesController`

- [ ] **Step 1: Update `ServiceDto.cs`**

```csharp
namespace ImperadorBarberShop.Application.DTOs;

public record ServiceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int DurationMinutes { get; init; }
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public string? PhotoUrl { get; init; }
    public List<ServiceDto> Addons { get; init; } = [];
}
```

- [ ] **Step 2: Update `MappingProfile.cs` — Service mapping now includes PhotoUrl**

`CreateMap<Service, ServiceDto>()` already maps by convention — `PhotoUrl` and `IsActive` map automatically. `Addons` is NOT auto-mapped (it comes from a separate query), so it stays at default `[]` when coming from AutoMapper. The handler will populate it manually.

- [ ] **Step 3: Update `GetServicesQuery.cs` — include add-ons**

```csharp
using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Services;

public record GetServicesQuery : IRequest<List<ServiceDto>>;

public class GetServicesQueryHandler : IRequestHandler<GetServicesQuery, List<ServiceDto>>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddonRepository _addonRepository;
    private readonly IMapper _mapper;

    public GetServicesQueryHandler(
        IServiceRepository serviceRepository,
        IServiceAddonRepository addonRepository,
        IMapper mapper)
    {
        _serviceRepository = serviceRepository;
        _addonRepository = addonRepository;
        _mapper = mapper;
    }

    public async Task<List<ServiceDto>> Handle(GetServicesQuery request, CancellationToken cancellationToken)
    {
        var services = await _serviceRepository.GetAllActiveAsync(cancellationToken);
        var serviceIds = services.Select(s => s.Id).ToList();
        var allAddons = await _addonRepository.GetByParentIdsAsync(serviceIds, cancellationToken);
        var addonsByParent = allAddons
            .GroupBy(a => a.ParentServiceId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.AddonService).ToList());

        return services.Select(s =>
        {
            var dto = _mapper.Map<ServiceDto>(s);
            var addons = addonsByParent.TryGetValue(s.Id, out var list)
                ? list.Select(a => _mapper.Map<ServiceDto>(a) with { Addons = [] }).ToList()
                : new List<ServiceDto>();
            return dto with { Addons = addons };
        }).ToList();
    }
}
```

- [ ] **Step 4: Create service CRUD commands**

**`CreateServiceCommand.cs`:**
```csharp
using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record CreateServiceCommand(
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes,
    string? PhotoUrl) : IRequest<Guid>;

public class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
    }
}

public class CreateServiceCommandHandler : IRequestHandler<CreateServiceCommand, Guid>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateServiceCommandHandler(IServiceRepository serviceRepository, IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = Service.Create(request.Name, request.Description, request.DurationMinutes, request.Price);
        if (request.PhotoUrl is not null) service.UpdatePhoto(request.PhotoUrl);
        await _serviceRepository.AddAsync(service, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return service.Id;
    }
}
```

**`UpdateServiceCommand.cs`** — follows same pattern; gets service by ID, calls `service.Update(...)` and optionally `service.UpdatePhoto(...)`, saves.

**`DeactivateServiceCommand.cs`** / **`ActivateServiceCommand.cs`** — same pattern as barber deactivate/activate; call `service.Deactivate()` / `service.Activate()`.

- [ ] **Step 5: Write failing test for add-on commands**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Services/ServiceAddonCommandHandlerTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Services;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class ServiceAddonCommandHandlerTests
{
    private readonly IServiceRepository _serviceRepo = Substitute.For<IServiceRepository>();
    private readonly IServiceAddonRepository _addonRepo = Substitute.For<IServiceAddonRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task AddAddon_SameId_Throws()
    {
        var id = Guid.NewGuid();
        var handler = new AddServiceAddonCommandHandler(_serviceRepo, _addonRepo, _uow);
        var act = () => handler.Handle(new AddServiceAddonCommand(id, id), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot*");
    }

    [Fact]
    public async Task AddAddon_AlreadyLinked_ThrowsConflict()
    {
        var parentId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var existingAddon = ServiceAddon.Create(parentId, addonId);
        _addonRepo.GetAsync(parentId, addonId, Arg.Any<CancellationToken>()).Returns(existingAddon);

        var handler = new AddServiceAddonCommandHandler(_serviceRepo, _addonRepo, _uow);
        var act = () => handler.Handle(new AddServiceAddonCommand(parentId, addonId), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already*");
    }
}
```

- [ ] **Step 6: Create `AddServiceAddonCommand.cs`**

```csharp
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record AddServiceAddonCommand(Guid ParentServiceId, Guid AddonServiceId) : IRequest;

public class AddServiceAddonCommandHandler : IRequestHandler<AddServiceAddonCommand>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddonRepository _addonRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddServiceAddonCommandHandler(
        IServiceRepository serviceRepository,
        IServiceAddonRepository addonRepository,
        IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _addonRepository = addonRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AddServiceAddonCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentServiceId == request.AddonServiceId)
            throw new ArgumentException("A service cannot be its own add-on.");

        var existing = await _addonRepository.GetAsync(
            request.ParentServiceId, request.AddonServiceId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("This add-on link already exists.");

        var addon = ServiceAddon.Create(request.ParentServiceId, request.AddonServiceId);
        await _addonRepository.AddAsync(addon, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

**`RemoveServiceAddonCommand.cs`** — same pattern; gets addon via `_addonRepository.GetAsync`, calls `_addonRepository.Remove(addon)`, saves.

- [ ] **Step 7: Add admin endpoints to `ServicesController.cs`**

Add `[Authorize(Policy = "RequireAdminRole")]` endpoints for POST, PUT, PATCH deactivate/activate, and the addon link/unlink actions. The controller injects `IImageService` and `IMediator`. Image upload handling follows the same `ValidateImage` pattern as `AdminController`.

```csharp
[HttpPost]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> CreateService([FromForm] CreateServiceRequest request, CancellationToken ct)
{
    string? photoUrl = null;
    if (request.Photo is not null)
    {
        ValidateImage(request.Photo);
        photoUrl = await _imageService.UploadAsync(
            request.Photo.OpenReadStream(), request.Photo.FileName, request.Photo.ContentType, ct);
    }
    var id = await _mediator.Send(
        new CreateServiceCommand(request.Name, request.Description, request.Price, request.DurationMinutes, photoUrl), ct);
    return CreatedAtAction(nameof(GetServices), new { id }, new { id });
}

[HttpPut("{id:guid}")]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> UpdateService(Guid id, [FromForm] UpdateServiceRequest request, CancellationToken ct)
{
    string? photoUrl = null;
    if (request.Photo is not null)
    {
        ValidateImage(request.Photo);
        photoUrl = await _imageService.UploadAsync(
            request.Photo.OpenReadStream(), request.Photo.FileName, request.Photo.ContentType, ct);
    }
    await _mediator.Send(
        new UpdateServiceCommand(id, request.Name, request.Description, request.Price, request.DurationMinutes, photoUrl), ct);
    return NoContent();
}

[HttpPatch("{id:guid}/deactivate")]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> DeactivateService(Guid id, CancellationToken ct)
{
    await _mediator.Send(new DeactivateServiceCommand(id), ct);
    return NoContent();
}

[HttpPatch("{id:guid}/activate")]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> ActivateService(Guid id, CancellationToken ct)
{
    await _mediator.Send(new ActivateServiceCommand(id), ct);
    return NoContent();
}

[HttpPost("{id:guid}/addons/{addonId:guid}")]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> AddAddon(Guid id, Guid addonId, CancellationToken ct)
{
    await _mediator.Send(new AddServiceAddonCommand(id, addonId), ct);
    return NoContent();
}

[HttpDelete("{id:guid}/addons/{addonId:guid}")]
[Authorize(Policy = "RequireAdminRole")]
public async Task<IActionResult> RemoveAddon(Guid id, Guid addonId, CancellationToken ct)
{
    await _mediator.Send(new RemoveServiceAddonCommand(id, addonId), ct);
    return NoContent();
}
```

Add `IImageService` to `ServicesController` constructor. Add helper:
```csharp
private static void ValidateImage(IFormFile file)
{
    if (file.Length > 5 * 1024 * 1024) throw new ArgumentException("Image must be smaller than 5 MB.");
    if (file.ContentType is not ("image/jpeg" or "image/png")) throw new ArgumentException("Only JPEG and PNG images are accepted.");
}
```

Add request records:
```csharp
public record CreateServiceRequest(string Name, string Description, decimal Price, int DurationMinutes, IFormFile? Photo);
public record UpdateServiceRequest(string Name, string Description, decimal Price, int DurationMinutes, IFormFile? Photo);
```

- [ ] **Step 8: Run unit tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "ServiceAddonCommandHandlerTests"
```
Expected: PASS.

- [ ] **Step 9: Run all tests**

```bash
cd backend && dotnet test
```

- [ ] **Step 10: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(api): service CRUD, add-on management, GET /services returns add-ons"
```

---

## Task 6: Financial dashboard (backend)

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/DTOs/FinancialDtos.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetFinancialSummaryQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetFinancialByBarberQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/GetFinancialByServiceQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Financial/ExportFinancialCsvQuery.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs`
- Test: `backend/tests/ImperadorBarberShop.UnitTests/Financial/GetFinancialSummaryQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IAppointmentRepository.GetCompletedByDateRangeAsync`
- Produces: `GET /admin/financial/summary`, `GET /admin/financial/by-barber`, `GET /admin/financial/by-service`, `GET /admin/financial/export`

- [ ] **Step 1: Create `FinancialDtos.cs`**

```csharp
namespace ImperadorBarberShop.Application.DTOs;

public record FinancialSummaryDto(decimal TotalRevenue, int TotalAppointments, decimal AverageTicket, DateOnly From, DateOnly To);

public record FinancialByBarberItemDto(Guid BarberId, string BarberName, int Appointments, decimal Revenue);

public record FinancialByServiceItemDto(Guid ServiceId, string ServiceName, int Count, decimal Revenue);
```

- [ ] **Step 2: Write failing summary test**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Financial/GetFinancialSummaryQueryHandlerTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class GetFinancialSummaryQueryHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();

    [Fact]
    public async Task Handle_NoAppointments_ReturnsZeros()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var handler = new GetFinancialSummaryQueryHandler(_repo);
        var query = new GetFinancialSummaryQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var result = await handler.Handle(query, CancellationToken.None);

        result.TotalRevenue.Should().Be(0);
        result.TotalAppointments.Should().Be(0);
        result.AverageTicket.Should().Be(0);
    }
}
```

- [ ] **Step 3: Create `GetFinancialSummaryQuery.cs`**

```csharp
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialSummaryQuery(DateOnly From, DateOnly To) : IRequest<FinancialSummaryDto>;

public class GetFinancialSummaryQueryHandler : IRequestHandler<GetFinancialSummaryQuery, FinancialSummaryDto>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialSummaryQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<FinancialSummaryDto> Handle(GetFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);
        var total = appointments.Count;
        var revenue = appointments
            .SelectMany(a => a.AppointmentServices)
            .Sum(s => s.Service.Price);
        var average = total > 0 ? revenue / total : 0m;

        return new FinancialSummaryDto(revenue, total, Math.Round(average, 2), request.From, request.To);
    }
}
```

- [ ] **Step 4: Create `GetFinancialByBarberQuery.cs`**

```csharp
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialByBarberQuery(DateOnly From, DateOnly To) : IRequest<List<FinancialByBarberItemDto>>;

public class GetFinancialByBarberQueryHandler : IRequestHandler<GetFinancialByBarberQuery, List<FinancialByBarberItemDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialByBarberQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<List<FinancialByBarberItemDto>> Handle(GetFinancialByBarberQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        return appointments
            .GroupBy(a => new { a.BarberId, Name = a.Barber.User.Name })
            .Select(g => new FinancialByBarberItemDto(
                g.Key.BarberId,
                g.Key.Name,
                g.Count(),
                g.SelectMany(a => a.AppointmentServices).Sum(s => s.Service.Price)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }
}
```

- [ ] **Step 5: Create `GetFinancialByServiceQuery.cs`** — same pattern, groups by `ServiceId` / `Service.Name`.

- [ ] **Step 6: Create `ExportFinancialCsvQuery.cs`**

```csharp
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;
using System.Text;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record ExportFinancialCsvQuery(DateOnly From, DateOnly To) : IRequest<string>;

public class ExportFinancialCsvQueryHandler : IRequestHandler<ExportFinancialCsvQuery, string>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public ExportFinancialCsvQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<string> Handle(ExportFinancialCsvQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Data,Barbeiro,Cliente,Telefone,Serviço,Preço,AgendamentoId");

        foreach (var a in appointments)
        {
            var date = a.ScheduledAt.ToString("yyyy-MM-dd");
            var barber = EscapeCsv(a.Barber.User.Name);
            var client = EscapeCsv(a.ClientName);
            var phone = MaskPhone(a.ClientPhone);

            foreach (var aps in a.AppointmentServices)
            {
                sb.AppendLine(
                    $"{date},{barber},{client},{phone},{EscapeCsv(aps.Service.Name)},{aps.Service.Price:F2},{a.Id}");
            }
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
        => value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    // Masks all but last 4 digits: +5511999990000 → +55119999****
    private static string MaskPhone(string phone)
        => phone.Length <= 4 ? phone : phone[..^4] + "****";
}
```

- [ ] **Step 7: Add financial endpoints to `AdminController.cs`**

```csharp
[HttpGet("financial/summary")]
public async Task<IActionResult> GetSummary(
    [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    => Ok(await _mediator.Send(new GetFinancialSummaryQuery(from, to), ct));

[HttpGet("financial/by-barber")]
public async Task<IActionResult> GetByBarber(
    [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    => Ok(await _mediator.Send(new GetFinancialByBarberQuery(from, to), ct));

[HttpGet("financial/by-service")]
public async Task<IActionResult> GetByService(
    [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    => Ok(await _mediator.Send(new GetFinancialByServiceQuery(from, to), ct));

[HttpGet("financial/export")]
public async Task<IActionResult> ExportCsv(
    [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
{
    var csv = await _mediator.Send(new ExportFinancialCsvQuery(from, to), ct);
    var bytes = Encoding.UTF8.GetBytes(csv);
    return File(bytes, "text/csv", $"relatorio-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.csv");
}
```

Add `using System.Text;` at the top of AdminController.

- [ ] **Step 8: Run tests**

```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests --filter "GetFinancialSummaryQueryHandlerTests"
cd backend && dotnet test
```

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(api): financial dashboard endpoints — summary, by-barber, by-service, CSV export"
```

---

## Task 7: Frontend foundation

**Files:**
- Add: `frontend/public/logo.png` (copy the skull logo file)
- Modify: `frontend/src/types/api.types.ts`
- Create: `frontend/src/lib/api/admin.api.ts`
- Modify: `frontend/src/lib/api/services.api.ts`
- Modify: `frontend/src/middleware.ts`
- Modify: `frontend/src/app/(auth)/login/page.tsx`
- Modify: `frontend/src/app/(auth)/login/LoginPageContent.tsx`
- Modify: `frontend/src/app/page.tsx`
- Modify: `frontend/src/app/(auth)/register/barber/page.tsx` (redirect)
- Test: `frontend/src/app/__tests__/HomePage.test.tsx`

**Interfaces:**
- Produces: updated types, admin API functions, middleware protecting `/admin/*`, home page with logo + barber button

- [ ] **Step 1: Copy logo**

Save the skull logo image as `frontend/public/logo.png`.

- [ ] **Step 2: Update `api.types.ts`**

```typescript
export type UserRole = 'Barber' | 'Admin'

// Add photoUrl to Service and Addons
export interface Service {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  isActive: boolean
  photoUrl: string | null
  addons: ServiceAddon[]
}

export interface ServiceAddon {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  photoUrl: string | null
}

// Add photoUrl and isActive to Barber
export interface Barber {
  id: string
  userId: string
  name: string
  email: string
  averageRating: number
  photoUrl: string | null
  isActive: boolean
  availability: BarberAvailability[]
}

// Admin barber (includes isActive)
export interface AdminBarber extends Barber {
  email: string
}

// Financial types
export interface FinancialSummary {
  totalRevenue: number
  totalAppointments: number
  averageTicket: number
  from: string
  to: string
}

export interface FinancialByBarberItem {
  barberId: string
  barberName: string
  appointments: number
  revenue: number
}

export interface FinancialByServiceItem {
  serviceId: string
  serviceName: string
  count: number
  revenue: number
}

// LoginResult: role can now be Admin
export interface LoginResult {
  accessToken: string
  refreshToken: string
  role: UserRole
  userId: string
  barberId: string | null
}

// Admin request types
export interface CreateBarberPayload {
  name: string
  email: string
  password: string
  availability: BarberAvailability[]
  photo?: File
}

export interface CreateServicePayload {
  name: string
  description: string
  price: number
  durationMinutes: number
  photo?: File
}

export interface UpdateServicePayload extends CreateServicePayload {
  id: string
}
```

Keep all existing types (`DayOfWeekString`, `BarberAvailability`, `Appointment`, `AppointmentManage`, `Review`, `LoginPayload`, `CreateAppointmentPayload`, `CreateAppointmentResult`, `CreateReviewByTokenPayload`, `CreateReviewByTokenResult`).

- [ ] **Step 3: Create `admin.api.ts`**

```typescript
import { apiClient } from './client'
import type {
  AdminBarber, FinancialSummary, FinancialByBarberItem,
  FinancialByServiceItem, CreateBarberPayload, CreateServicePayload,
  UpdateServicePayload, Service
} from '@/types/api.types'

export const adminApi = {
  // Barbers
  getBarbers: () =>
    apiClient.get<AdminBarber[]>('/admin/barbers').then(r => r.data),

  createBarber: (payload: CreateBarberPayload) => {
    const form = new FormData()
    form.append('name', payload.name)
    form.append('email', payload.email)
    form.append('password', payload.password)
    payload.availability.forEach((a, i) => {
      form.append(`availability[${i}].dayOfWeek`, a.dayOfWeek)
      form.append(`availability[${i}].startTime`, a.startTime)
      form.append(`availability[${i}].endTime`, a.endTime)
    })
    if (payload.photo) form.append('photo', payload.photo)
    return apiClient.post<{ id: string }>('/admin/barbers', form).then(r => r.data)
  },

  deactivateBarber: (id: string) =>
    apiClient.patch(`/admin/barbers/${id}/deactivate`),

  activateBarber: (id: string) =>
    apiClient.patch(`/admin/barbers/${id}/activate`),

  // Password
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.patch('/admin/profile/password', { currentPassword, newPassword }),

  // Financial
  getSummary: (from: string, to: string) =>
    apiClient.get<FinancialSummary>('/admin/financial/summary', { params: { from, to } }).then(r => r.data),

  getByBarber: (from: string, to: string) =>
    apiClient.get<FinancialByBarberItem[]>('/admin/financial/by-barber', { params: { from, to } }).then(r => r.data),

  getByService: (from: string, to: string) =>
    apiClient.get<FinancialByServiceItem[]>('/admin/financial/by-service', { params: { from, to } }).then(r => r.data),

  exportCsvUrl: (from: string, to: string) =>
    `/api/v1/admin/financial/export?from=${from}&to=${to}`,
}

export const adminServicesApi = {
  createService: (payload: CreateServicePayload) => {
    const form = new FormData()
    form.append('name', payload.name)
    form.append('description', payload.description)
    form.append('price', String(payload.price))
    form.append('durationMinutes', String(payload.durationMinutes))
    if (payload.photo) form.append('photo', payload.photo)
    return apiClient.post<{ id: string }>('/services', form).then(r => r.data)
  },

  updateService: (payload: UpdateServicePayload) => {
    const form = new FormData()
    form.append('name', payload.name)
    form.append('description', payload.description)
    form.append('price', String(payload.price))
    form.append('durationMinutes', String(payload.durationMinutes))
    if (payload.photo) form.append('photo', payload.photo)
    return apiClient.put(`/services/${payload.id}`, form)
  },

  deactivateService: (id: string) => apiClient.patch(`/services/${id}/deactivate`),
  activateService: (id: string) => apiClient.patch(`/services/${id}/activate`),

  addAddon: (serviceId: string, addonId: string) =>
    apiClient.post(`/services/${serviceId}/addons/${addonId}`),

  removeAddon: (serviceId: string, addonId: string) =>
    apiClient.delete(`/services/${serviceId}/addons/${addonId}`),
}
```

- [ ] **Step 4: Update `middleware.ts`**

```typescript
import { NextResponse, type NextRequest } from 'next/server'

const ROLE_COOKIE = 'imperador_access_role'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  const role = request.cookies.get(ROLE_COOKIE)?.value

  if (pathname.startsWith('/admin')) {
    if (role !== 'Admin') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  if (pathname.startsWith('/barber')) {
    if (role !== 'Barber') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/barber/:path*', '/admin/:path*'],
}
```

- [ ] **Step 5: Update `AuthProvider` to redirect Admin after login**

In `AuthProvider.tsx`, find where login sets the cookie and redirects. Add handling for `role === 'Admin'` → push `/admin/dashboard`. Currently it redirects Barber to `/barber/dashboard`. Replicate pattern for Admin.

- [ ] **Step 6: Update login page — remove register link, handle Admin**

In `frontend/src/app/(auth)/login/page.tsx`: remove the "Criar conta de barbeiro" / "Sou barbeiro" link if present. The `LoginPageContent.tsx` shows a "just registered" banner — remove the `justRegistered` query param logic (registration is gone).

- [ ] **Step 7: Update home page `page.tsx`**

Replace the existing CTA section and add logo + barber area button:

```tsx
// In the hero section, add logo above the heading:
<Image
  src="/logo.png"
  alt="O Imperador Barber Shop"
  width={160}
  height={160}
  className="mb-2"
  priority
/>

// In the hero buttons div, add the barber area button:
<Link href="/login">
  <Button variant="secondary" size="lg" className="min-w-[200px]">
    Área do Barbeiro
  </Button>
</Link>

// In the bottom CTA section, replace "Sou barbeiro" link with:
<Link href="/login">
  <Button variant="secondary" size="lg">
    Área do Barbeiro
  </Button>
</Link>
```

Import `Image` from `'next/image'`.

- [ ] **Step 8: Replace `/register/barber` page with redirect**

```tsx
// frontend/src/app/(auth)/register/barber/page.tsx
import { redirect } from 'next/navigation'
export default function RegisterBarberPage() {
  redirect('/login')
}
```

- [ ] **Step 9: Write component test for home page**

```tsx
// frontend/src/app/__tests__/HomePage.test.tsx
import { render, screen } from '@testing-library/react'
import LandingPage from '@/app/page'

describe('LandingPage', () => {
  it('renders logo', () => {
    render(<LandingPage />)
    expect(screen.getByAltText('O Imperador Barber Shop')).toBeInTheDocument()
  })

  it('renders Área do Barbeiro link to /login', () => {
    render(<LandingPage />)
    const links = screen.getAllByRole('link', { name: /área do barbeiro/i })
    links.forEach(link => expect(link).toHaveAttribute('href', '/login'))
  })

  it('does not render Sou barbeiro link', () => {
    render(<LandingPage />)
    expect(screen.queryByText(/sou barbeiro/i)).not.toBeInTheDocument()
  })
})
```

- [ ] **Step 10: Run frontend tests**

```bash
cd frontend && npm test -- --testPathPattern="HomePage"
```

- [ ] **Step 11: Commit**

```bash
git add frontend/src frontend/public
git commit -m "feat(frontend): logo, Área do Barbeiro button, Admin routing, remove barber self-registration"
```

---

## Task 8: Frontend admin area

**Files:**
- Create: `frontend/src/hooks/useAdminBarbers.ts`
- Create: `frontend/src/hooks/useAdminServices.ts`
- Create: `frontend/src/hooks/useAdminFinancial.ts`
- Create: `frontend/src/app/admin/layout.tsx`
- Create: `frontend/src/app/admin/dashboard/page.tsx`
- Create: `frontend/src/app/admin/barbers/page.tsx`
- Create: `frontend/src/app/admin/services/page.tsx`
- Test: `frontend/src/app/admin/__tests__/DashboardPage.test.tsx`

**Interfaces:**
- Consumes: `adminApi`, `adminServicesApi` from `admin.api.ts`
- Produces: `/admin/dashboard`, `/admin/barbers`, `/admin/services`

- [ ] **Step 1: Create hooks**

**`useAdminBarbers.ts`:**
```typescript
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'
import type { CreateBarberPayload } from '@/types/api.types'

export function useAdminBarbers() {
  return useQuery({ queryKey: ['admin', 'barbers'], queryFn: adminApi.getBarbers })
}

export function useCreateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBarberPayload) => adminApi.createBarber(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}

export function useDeactivateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.deactivateBarber(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}

export function useActivateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.activateBarber(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}
```

**`useAdminServices.ts`:** same pattern using `adminServicesApi` — `useAdminAllServices` (calls `GET /services`, returns all including inactive for admin), `useCreateService`, `useUpdateService`, `useDeactivateService`, `useActivateService`, `useAddAddon`, `useRemoveAddon`.

**`useAdminFinancial.ts`:**
```typescript
import { useQuery } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'

export function useFinancialSummary(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'summary', from, to],
    queryFn: () => adminApi.getSummary(from, to),
    enabled: !!from && !!to,
  })
}

export function useFinancialByBarber(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'barber', from, to],
    queryFn: () => adminApi.getByBarber(from, to),
    enabled: !!from && !!to,
  })
}

export function useFinancialByService(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'service', from, to],
    queryFn: () => adminApi.getByService(from, to),
    enabled: !!from && !!to,
  })
}
```

- [ ] **Step 2: Create admin layout**

```tsx
// frontend/src/app/admin/layout.tsx
import Image from 'next/image'
import Link from 'next/link'
import { LogoutButton } from '@/components/auth/LogoutButton'

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      {/* Sidebar */}
      <aside className="w-64 bg-brand-black-soft border-r border-brand-white/10 flex flex-col p-6 gap-6">
        <div className="flex flex-col items-center gap-3">
          <Image src="/logo.png" alt="O Imperador" width={80} height={80} />
          <span className="font-montserrat text-sm font-semibold text-brand-gold uppercase tracking-widest">
            Administrador
          </span>
        </div>

        <nav className="flex flex-col gap-2 flex-1">
          {[
            { href: '/admin/dashboard', label: 'Dashboard' },
            { href: '/admin/barbers', label: 'Barbeiros' },
            { href: '/admin/services', label: 'Serviços' },
          ].map(({ href, label }) => (
            <Link
              key={href}
              href={href}
              className="rounded-lg px-4 py-2 text-sm text-brand-white/70 hover:bg-brand-gold/10 hover:text-brand-gold transition-colors"
            >
              {label}
            </Link>
          ))}
        </nav>

        <LogoutButton />
      </aside>

      <main className="flex-1 p-8">{children}</main>
    </div>
  )
}
```

- [ ] **Step 3: Create dashboard page**

```tsx
// frontend/src/app/admin/dashboard/page.tsx
'use client'

import { useState } from 'react'
import { useFinancialSummary, useFinancialByBarber, useFinancialByService } from '@/hooks/useAdminFinancial'
import { adminApi } from '@/lib/api/admin.api'

const PRESETS = [
  { label: 'Hoje', getDates: () => { const d = today(); return { from: d, to: d } } },
  { label: 'Esta semana', getDates: () => ({ from: weekStart(), to: today() }) },
  { label: 'Este mês', getDates: () => ({ from: monthStart(), to: today() }) },
]

function today() { return new Date().toISOString().slice(0, 10) }
function weekStart() {
  const d = new Date(); d.setDate(d.getDate() - d.getDay())
  return d.toISOString().slice(0, 10)
}
function monthStart() {
  const d = new Date(); d.setDate(1)
  return d.toISOString().slice(0, 10)
}

export default function DashboardPage() {
  const [from, setFrom] = useState(monthStart)
  const [to, setTo] = useState(today)

  const { data: summary } = useFinancialSummary(from, to)
  const { data: byBarber } = useFinancialByBarber(from, to)
  const { data: byService } = useFinancialByService(from, to)

  const fmt = (n: number) =>
    n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

  return (
    <div className="space-y-8">
      <h1 className="font-montserrat text-2xl font-black text-brand-white">Dashboard Financeiro</h1>

      {/* Period selector */}
      <div className="flex flex-wrap gap-3 items-center">
        {PRESETS.map(p => (
          <button
            key={p.label}
            onClick={() => { const d = p.getDates(); setFrom(d.from); setTo(d.to) }}
            className="px-4 py-2 rounded-lg border border-brand-gold/30 text-sm text-brand-gold hover:bg-brand-gold/10 transition-colors"
          >
            {p.label}
          </button>
        ))}
        <input type="date" value={from} onChange={e => setFrom(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm" />
        <span className="text-brand-white/50">até</span>
        <input type="date" value={to} onChange={e => setTo(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm" />
        <a
          href={`${process.env.NEXT_PUBLIC_API_URL}${adminApi.exportCsvUrl(from, to)}`}
          download
          className="ml-auto px-4 py-2 rounded-lg bg-brand-gold text-brand-black text-sm font-semibold hover:bg-brand-gold-light transition-colors"
        >
          Exportar CSV
        </a>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[
          { label: 'Receita Total', value: fmt(summary?.totalRevenue ?? 0) },
          { label: 'Atendimentos', value: String(summary?.totalAppointments ?? 0) },
          { label: 'Ticket Médio', value: fmt(summary?.averageTicket ?? 0) },
        ].map(({ label, value }) => (
          <div key={label} className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6">
            <p className="text-sm text-brand-white/50">{label}</p>
            <p className="font-montserrat text-2xl font-black text-brand-gold mt-1">{value}</p>
          </div>
        ))}
      </div>

      {/* By Barber table */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Por Barbeiro</h2>
        <table className="w-full text-sm text-brand-white/80">
          <thead><tr className="border-b border-brand-white/10 text-left text-brand-white/40">
            <th className="pb-2">Barbeiro</th>
            <th className="pb-2">Atendimentos</th>
            <th className="pb-2">Receita</th>
          </tr></thead>
          <tbody>
            {byBarber?.map(row => (
              <tr key={row.barberId} className="border-b border-brand-white/5">
                <td className="py-2">{row.barberName}</td>
                <td className="py-2">{row.appointments}</td>
                <td className="py-2 text-brand-gold">{fmt(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* By Service table */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Por Serviço</h2>
        <table className="w-full text-sm text-brand-white/80">
          <thead><tr className="border-b border-brand-white/10 text-left text-brand-white/40">
            <th className="pb-2">Serviço</th>
            <th className="pb-2">Vendas</th>
            <th className="pb-2">Receita</th>
          </tr></thead>
          <tbody>
            {byService?.map(row => (
              <tr key={row.serviceId} className="border-b border-brand-white/5">
                <td className="py-2">{row.serviceName}</td>
                <td className="py-2">{row.count}</td>
                <td className="py-2 text-brand-gold">{fmt(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  )
}
```

- [ ] **Step 4: Create barbers page**

`frontend/src/app/admin/barbers/page.tsx` — list of barbers with photo (or initials placeholder), name, email, average rating, active/inactive badge. "Adicionar Barbeiro" button opens a modal with a form (React Hook Form + Zod) for name, email, password, availability, photo upload. Deactivate/Activate buttons per row call the respective mutations.

Availability input reuses the same `AvailabilityPicker` component from the old registration page if it exists, or a simple list of day + start/end time inputs.

- [ ] **Step 5: Create services page**

`frontend/src/app/admin/services/page.tsx` — list of services with photo, name, price, duration, active/inactive badge. "Adicionar Serviço" button opens a form. Each service row has an "Add-ons" button that opens a modal showing all other active services as checkboxes — checked = currently linked as add-on. Toggling calls `useAddAddon` / `useRemoveAddon`.

- [ ] **Step 6: Write dashboard test**

```tsx
// frontend/src/app/admin/__tests__/DashboardPage.test.tsx
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import DashboardPage from '@/app/admin/dashboard/page'
import { http, HttpResponse } from 'msw'
import { server } from '@/mocks/server'

const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
const Wrapper = ({ children }: { children: React.ReactNode }) =>
  <QueryClientProvider client={qc}>{children}</QueryClientProvider>

beforeEach(() => {
  server.use(
    http.get('*/admin/financial/summary', () =>
      HttpResponse.json({ totalRevenue: 250, totalAppointments: 5, averageTicket: 50, from: '2026-07-01', to: '2026-07-31' })),
    http.get('*/admin/financial/by-barber', () => HttpResponse.json([])),
    http.get('*/admin/financial/by-service', () => HttpResponse.json([])),
  )
})

it('renders summary cards', async () => {
  render(<DashboardPage />, { wrapper: Wrapper })
  expect(await screen.findByText(/R\$\s*250/)).toBeInTheDocument()
  expect(screen.getByText('5')).toBeInTheDocument()
})

it('renders export CSV link', () => {
  render(<DashboardPage />, { wrapper: Wrapper })
  expect(screen.getByRole('link', { name: /exportar csv/i })).toBeInTheDocument()
})
```

- [ ] **Step 7: Run frontend tests**

```bash
cd frontend && npm test -- --testPathPattern="DashboardPage|admin"
```

- [ ] **Step 8: Commit**

```bash
git add frontend/src
git commit -m "feat(frontend): admin area — dashboard, barbers, services pages + hooks"
```

---

## Task 9: Frontend booking wizard add-ons

**Files:**
- Modify: `frontend/src/app/agendar/page.tsx`
- Test: `frontend/src/app/agendar/__tests__/AddonSelection.test.tsx`

**Interfaces:**
- Consumes: `Service.addons` from updated `GET /services`
- Produces: add-on selection UI in step 2 of the booking wizard; add-on IDs included in `serviceIds`

- [ ] **Step 1: Write failing test**

```tsx
// frontend/src/app/agendar/__tests__/AddonSelection.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { ServicePicker } from '@/components/booking/ServicePicker'
import type { Service } from '@/types/api.types'

const mockService: Service = {
  id: 'svc-1', name: 'Cabelo Masculino', description: '', durationMinutes: 30,
  price: 35, isActive: true, photoUrl: null,
  addons: [
    { id: 'addon-1', name: 'Barba', description: '', durationMinutes: 20, price: 25, photoUrl: null },
  ],
}

it('shows add-on section when service with addons is selected', () => {
  render(
    <ServicePicker
      services={[mockService]}
      selectedIds={['svc-1']}
      onToggle={() => {}}
    />
  )
  expect(screen.getByText('Deseja adicionar?')).toBeInTheDocument()
  expect(screen.getByText('Barba')).toBeInTheDocument()
})

it('does not show add-on section when no addons', () => {
  const noAddon: Service = { ...mockService, addons: [] }
  render(
    <ServicePicker
      services={[noAddon]}
      selectedIds={['svc-1']}
      onToggle={() => {}}
    />
  )
  expect(screen.queryByText('Deseja adicionar?')).not.toBeInTheDocument()
})

it('updates total price when addon toggled', () => {
  let ids: string[] = ['svc-1']
  const { rerender } = render(
    <ServicePicker services={[mockService]} selectedIds={ids} onToggle={id => { ids = [...ids, id] }} />
  )
  fireEvent.click(screen.getByRole('checkbox', { name: /barba/i }))
  rerender(
    <ServicePicker services={[mockService]} selectedIds={ids} onToggle={() => {}} />
  )
  expect(screen.getByText(/R\$\s*60/)).toBeInTheDocument()
})
```

- [ ] **Step 2: Run — expect FAIL**

```bash
cd frontend && npm test -- --testPathPattern="AddonSelection" 2>&1 | tail -5
```

- [ ] **Step 3: Update `ServicePicker` component**

In `frontend/src/components/booking/ServicePicker.tsx`, after rendering each selected service that has `addons.length > 0`, add:

```tsx
{selectedIds.includes(service.id) && service.addons.length > 0 && (
  <div className="mt-2 ml-4 space-y-2">
    <p className="text-xs font-semibold text-brand-gold/80 uppercase tracking-widest">
      Deseja adicionar?
    </p>
    {service.addons.map(addon => (
      <label
        key={addon.id}
        className="flex items-center gap-3 cursor-pointer rounded-lg border border-brand-white/10 bg-brand-black p-3 hover:border-brand-gold/30 transition-colors"
      >
        <input
          type="checkbox"
          aria-label={addon.name}
          checked={selectedIds.includes(addon.id)}
          onChange={() => onToggle(addon.id)}
          className="accent-brand-gold"
        />
        {addon.photoUrl && (
          <img src={addon.photoUrl} alt={addon.name} className="w-8 h-8 rounded object-cover" />
        )}
        <span className="flex-1 text-sm text-brand-white">{addon.name}</span>
        <span className="text-sm text-brand-gold">+{addon.durationMinutes}min</span>
        <span className="text-sm text-brand-gold font-semibold">
          R$ {addon.price.toFixed(2).replace('.', ',')}
        </span>
      </label>
    ))}
  </div>
)}
```

Update the total price/duration display (already rendered from `selectedServices`) — since add-on IDs are in `selectedServiceIds` and `allServices` includes both services and add-ons, the existing `selectedServices.reduce(...)` total already works. No change needed to the total calculation in `agendar/page.tsx`.

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd frontend && npm test -- --testPathPattern="AddonSelection"
```

- [ ] **Step 5: Run full frontend test suite**

```bash
cd frontend && npm test
```

- [ ] **Step 6: Run all backend tests**

```bash
cd backend && dotnet test
```

- [ ] **Step 7: Final commit**

```bash
git add frontend/src
git commit -m "feat(frontend): add-on selection in booking wizard step 2"
```

---

## Self-review checklist

After all tasks are complete, verify:

- [ ] `GET /services` returns `addons` array (test with Swagger or `curl`)
- [ ] Admin login redirects to `/admin/dashboard`
- [ ] Barber login still redirects to `/barber/dashboard`
- [ ] `/register/barber` redirects to `/login` (no 404)
- [ ] `/admin/*` returns 302 to `/login` when not authenticated
- [ ] Admin seed does not create duplicate admin on restart
- [ ] `POST /admin/barbers` with photo stores Cloudinary URL in DB
- [ ] Financial export CSV is masked (phone shows last 4 digits only)
- [ ] Add-on selection in booking wizard includes add-on IDs in `serviceIds`
- [ ] Logo renders on home page and in admin sidebar
