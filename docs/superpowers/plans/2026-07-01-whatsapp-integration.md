# Spec 3 — WhatsApp Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add WhatsApp notifications (via Evolution API) to all appointment lifecycle events, with admin-managed connection and configurable email/WhatsApp channels.

**Architecture:** `INotificationService` orchestrates email + WhatsApp based on an `AppSettings` DB table. `EvolutionApiWhatsAppService` calls Evolution API via `HttpClient`. `ReminderBackgroundService` polls every 60s for appointments needing a reminder. All notification failures are silenced by the calling handlers (best-effort). Four existing appointment handlers are updated to call `INotificationService`. Five new admin endpoints manage the WhatsApp connection and notification config.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, MediatR, xUnit + NSubstitute + FluentAssertions, `IHttpClientFactory`, `IHostedService`; Next.js 15, TanStack Query v5, Vitest + React Testing Library, MSW v2

## Global Constraints

- Notification failures MUST NOT roll back appointment state — handlers wrap notification calls in `try/catch`
- All UI text in Brazilian Portuguese
- `ClientPhone` is always `+55DDDXXXXXXXXX` — pass directly to WhatsApp API without transformation
- `FrontendUrl` read from `IConfiguration["FrontendUrl"]` (already in appsettings)
- Time display: `scheduledAt.AddHours(-3).ToString("HH:mm")` — hardcoded UTC-3 (Brasília)
- Date display: `scheduledAt.AddHours(-3).ToString("dd/MM/yyyy")`
- Brand tokens: `brand-gold`, `brand-black`, `brand-black-soft`, `brand-white`
- Evolution API v2 headers: `apikey: <value>` on every request
- Evolution API config + barber notification phone live in `AppSettings` DB table, not env vars (env vars bootstrap on first run only)
- `AppSettings` keys used: `notifications:channels`, `notifications:reminderMinutesBefore`, `whatsapp:evolutionApiUrl`, `whatsapp:evolutionApiKey`, `whatsapp:instanceName`, `whatsapp:notificationPhone`

---

## File Map

### New — Backend
| File | Purpose |
|------|---------|
| `Domain/Entities/AppSettings.cs` | Key-Value entity |
| `Domain/Interfaces/IAppSettingsRepository.cs` | Repo contract |
| `Application/Interfaces/IWhatsAppService.cs` | WhatsApp service contract + enums |
| `Application/Interfaces/INotificationService.cs` | Orchestrator contract |
| `Infrastructure/Persistence/Configurations/AppSettingsConfiguration.cs` | EF config |
| `Infrastructure/Persistence/Repositories/AppSettingsRepository.cs` | EF repo |
| `Infrastructure/Services/EvolutionApiWhatsAppService.cs` | HTTP client impl |
| `Infrastructure/Services/NotificationService.cs` | Channel orchestrator impl |
| `Infrastructure/Services/ReminderBackgroundService.cs` | 60s polling background job |
| `Application/Queries/Admin/GetWhatsAppStatusQuery.cs` | MediatR query |
| `Application/Queries/Admin/GetWhatsAppQrQuery.cs` | MediatR query |
| `Application/Queries/Admin/GetNotificationSettingsQuery.cs` | MediatR query |
| `Application/Commands/Admin/DisconnectWhatsAppCommand.cs` | MediatR command |
| `Application/Commands/Admin/UpdateNotificationSettingsCommand.cs` | MediatR command |
| `UnitTests/Services/NotificationServiceTests.cs` | Channel dispatch tests |
| `UnitTests/Services/EvolutionApiWhatsAppServiceTests.cs` | HTTP client tests |

### Modified — Backend
| File | Change |
|------|--------|
| `Domain/Entities/Appointment.cs` | Add `ReminderSentAt`, `MarkReminderSent()` |
| `Domain/Interfaces/IAppointmentRepository.cs` | Add `GetPendingRemindersAsync` |
| `Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs` | Map `ReminderSentAt` |
| `Infrastructure/Persistence/AppDbContext.cs` | Add `AppSettings` DbSet |
| `Infrastructure/Persistence/Repositories/AppointmentRepository.cs` | Implement `GetPendingRemindersAsync` |
| `Infrastructure/DependencyInjection.cs` | Register new services + HttpClient |
| `Api/Program.cs` | Bootstrap AppSettings from env vars |
| `Api/Controllers/AdminController.cs` | 5 new endpoints |
| `Application/Commands/Appointments/CreateAppointmentCommand.cs` | Use `INotificationService` |
| `Application/Commands/Appointments/CancelAppointmentByTokenCommand.cs` | Add notification |
| `Application/Commands/Appointments/CancelAppointmentByBarberCommand.cs` | Add notification |
| `Application/Commands/Appointments/CompleteAppointmentCommand.cs` | Add notification |
| `IntegrationTests/WebAppFixture.cs` | Register fake `IWhatsAppService` |

### New — Frontend
| File | Purpose |
|------|---------|
| `lib/api/whatsapp.api.ts` | API client functions |
| `hooks/useWhatsApp.ts` | TanStack Query hooks |
| `app/admin/whatsapp/page.tsx` | Page with two tabs |
| `tests/unit/app/admin/WhatsAppPage.test.tsx` | Component tests |

### Modified — Frontend
| File | Change |
|------|--------|
| `types/api.types.ts` | Add WhatsApp + notification types |
| `app/admin/layout.tsx` | Add "WhatsApp" nav link |

---

## Task 1: Domain — AppSettings entity + Appointment.MarkReminderSent()

**Files:**
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/AppSettings.cs`
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Appointment.cs`

**Produces:** `AppSettings` with `Key`, `Value`, `SetValue()`; `Appointment.ReminderSentAt (DateTime?)` and `MarkReminderSent()`

- [ ] **Step 1: Create AppSettings.cs**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Entities/AppSettings.cs
namespace ImperadorBarberShop.Domain.Entities;

public class AppSettings
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    private AppSettings() { }

    public static AppSettings Create(string key, string value) => new() { Key = key, Value = value };

    public void SetValue(string value) => Value = value;
}
```

- [ ] **Step 2: Add ReminderSentAt + MarkReminderSent() to Appointment**

In `backend/src/Domain/ImperadorBarberShop.Domain/Entities/Appointment.cs`:

After `public DateTime UpdatedAt { get; private set; }` add:
```csharp
    public DateTime? ReminderSentAt { get; private set; }
```

After the `Complete()` method, add:
```csharp
    public void MarkReminderSent()
    {
        ReminderSentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
```

- [ ] **Step 3: Build to verify no compile errors**
```bash
cd backend
dotnet build src/Domain/ImperadorBarberShop.Domain
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**
```bash
git add backend/src/Domain/
git commit -m "feat(domain): AppSettings entity + Appointment.MarkReminderSent"
```

---

## Task 2: Application & Domain interfaces

**Files:**
- Create: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppSettingsRepository.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Interfaces/IWhatsAppService.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Interfaces/INotificationService.cs`

**Produces:** Three contracts used by all subsequent tasks. `INotificationService.SendAppointmentCreatedAsync` takes explicit `barber` + `services` parameters because the appointment entity is newly created and its navigation properties are not yet loaded from DB. The other three methods receive appointments already loaded from DB (`Include(a => a.Barber).ThenInclude(b => b.User)` + `Include(a => a.AppointmentServices).ThenInclude(s => s.Service)`).

- [ ] **Step 1: Create IAppSettingsRepository.cs**

```csharp
// backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppSettingsRepository.cs
namespace ImperadorBarberShop.Domain.Interfaces;

public interface IAppSettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Create IWhatsAppService.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Interfaces/IWhatsAppService.cs
namespace ImperadorBarberShop.Application.Interfaces;

public enum WhatsAppConnectionStatus { Connected, Disconnected, QrRequired }

public record WhatsAppStatus(WhatsAppConnectionStatus Status, string? PhoneNumber);

public record WhatsAppQr(string QrCode);

public interface IWhatsAppService
{
    Task SendAsync(string phone, string message, CancellationToken ct = default);
    Task<WhatsAppStatus> GetStatusAsync(CancellationToken ct = default);
    Task<WhatsAppQr> GetQrCodeAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Create INotificationService.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Interfaces/INotificationService.cs
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Application.Interfaces;

public interface INotificationService
{
    Task SendAppointmentCreatedAsync(
        Appointment appointment, Barber barber, List<Service> services, CancellationToken ct = default);
    Task SendAppointmentCancelledAsync(Appointment appointment, CancellationToken ct = default);
    Task SendAppointmentCompletedAsync(Appointment appointment, CancellationToken ct = default);
    Task SendReminderAsync(Appointment appointment, CancellationToken ct = default);
}
```

- [ ] **Step 4: Commit**
```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppSettingsRepository.cs \
        backend/src/Application/ImperadorBarberShop.Application/Interfaces/
git commit -m "feat(interfaces): IAppSettingsRepository, IWhatsAppService, INotificationService"
```

---

## Task 3: Infrastructure — DB setup (EF config, repo, migration, DI, env bootstrap)

**Files:**
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/AppSettingsConfiguration.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppSettingsRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Program.cs`

**Consumes:** `AppSettings` and `IAppSettingsRepository` from Tasks 1–2

- [ ] **Step 1: Create AppSettingsConfiguration.cs**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/AppSettingsConfiguration.cs
using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Value).IsRequired().HasMaxLength(500);
    }
}
```

- [ ] **Step 2: Create AppSettingsRepository.cs**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppSettingsRepository.cs
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly AppDbContext _context;

    public AppSettingsRepository(AppDbContext context) => _context = context;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => (await _context.AppSettings.FindAsync([key], ct))?.Value;

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await _context.AppSettings.FindAsync([key], ct);
        if (setting is null)
            await _context.AppSettings.AddAsync(AppSettings.Create(key, value), ct);
        else
        {
            setting.SetValue(value);
            _context.AppSettings.Update(setting);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
        => await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);
}
```

- [ ] **Step 3: Update AppointmentConfiguration.cs — map ReminderSentAt**

In `Configure`, after `builder.Property(a => a.UpdatedAt).IsRequired();` add:
```csharp
        builder.Property(a => a.ReminderSentAt);
```

- [ ] **Step 4: Update AppDbContext.cs — add AppSettings DbSet**

After `public DbSet<ServiceAddon> ServiceAddons => Set<ServiceAddon>();` add:
```csharp
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
```

- [ ] **Step 5: Update DependencyInjection.cs — register AppSettingsRepository**

After `services.AddScoped<IServiceAddonRepository, ServiceAddonRepository>();` add:
```csharp
        services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
        services.AddHttpClient();
```

- [ ] **Step 6: Bootstrap AppSettings from env vars in Program.cs**

Find where `adminSeed.SeedAdminAsync(app.Logger);` is called and add this block immediately after:

```csharp
// Bootstrap AppSettings defaults and Evolution API config from env vars on first run
using (var settingsScope = app.Services.CreateScope())
{
    var settingsRepo = settingsScope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
    var existing = await settingsRepo.GetAllAsync();

    var defaults = new Dictionary<string, string>
    {
        ["notifications:channels"] = "email,whatsapp",
        ["notifications:reminderMinutesBefore"] = "60",
    };
    foreach (var (key, val) in defaults)
        if (!existing.ContainsKey(key))
            await settingsRepo.SetAsync(key, val);

    var envMappings = new Dictionary<string, string>
    {
        ["whatsapp:evolutionApiUrl"]  = "WHATSAPP__EVOLUTIONAPIURL",
        ["whatsapp:evolutionApiKey"]  = "WHATSAPP__EVOLUTIONAPIKEY",
        ["whatsapp:instanceName"]     = "WHATSAPP__INSTANCENAME",
    };
    foreach (var (key, envVar) in envMappings)
    {
        var envVal = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(envVal) && !existing.ContainsKey(key))
            await settingsRepo.SetAsync(key, envVal);
    }
}
```

- [ ] **Step 7: Generate and apply migration**
```bash
cd backend
dotnet ef migrations add AddAppSettingsAndReminderSentAt \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api

dotnet ef database update \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```
Expected: migration file created, DB updated.

- [ ] **Step 8: Build to verify**
```bash
cd backend && dotnet build src/Api/ImperadorBarberShop.Api
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 9: Commit**
```bash
git add backend/src/Infrastructure/ backend/src/Api/ImperadorBarberShop.Api/Program.cs
git commit -m "feat(infra): AppSettings table + ReminderSentAt migration, repo, DI"
```

---

## Task 4: EvolutionApiWhatsAppService + unit tests

**Files:**
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/EvolutionApiWhatsAppService.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Services/EvolutionApiWhatsAppServiceTests.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`

**Consumes:** `IWhatsAppService`, `IAppSettingsRepository` from Task 2

Evolution API v2 endpoints:
- Send: `POST {baseUrl}/message/sendText/{instance}` body `{"number":"+55...","text":"..."}` header `apikey: <key>`
- Status: `GET {baseUrl}/instance/connectionState/{instance}` → `{"instance":{"state":"open"|"close"|"connecting","profileName":"..."}}`
- QR: `GET {baseUrl}/instance/connect/{instance}` → `{"base64":"data:image/png;base64,..."}`
- Disconnect: `DELETE {baseUrl}/instance/logout/{instance}`

When config keys are missing from AppSettings, all methods throw `InvalidOperationException`.

- [ ] **Step 1: Write failing tests**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Services/EvolutionApiWhatsAppServiceTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using NSubstitute;
using System.Net;

namespace ImperadorBarberShop.UnitTests.Services;

public class EvolutionApiWhatsAppServiceTests
{
    private readonly IAppSettingsRepository _settingsRepo = Substitute.For<IAppSettingsRepository>();
    private readonly FakeHttpMessageHandler _fakeHandler = new();
    private readonly EvolutionApiWhatsAppService _svc;

    public EvolutionApiWhatsAppServiceTests()
    {
        _settingsRepo.GetAsync("whatsapp:evolutionApiUrl",  Arg.Any<CancellationToken>()).Returns("http://evo.local");
        _settingsRepo.GetAsync("whatsapp:evolutionApiKey",  Arg.Any<CancellationToken>()).Returns("key123");
        _settingsRepo.GetAsync("whatsapp:instanceName",     Arg.Any<CancellationToken>()).Returns("imperador");
        _svc = new EvolutionApiWhatsAppService(new HttpClient(_fakeHandler), _settingsRepo);
    }

    [Fact]
    public async Task SendAsync_PostsToCorrectEndpoint()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, "{}");
        await _svc.SendAsync("+5511999990000", "Olá!", CancellationToken.None);
        _fakeHandler.LastRequest!.RequestUri!.ToString()
            .Should().Be("http://evo.local/message/sendText/imperador");
        _fakeHandler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetStatusAsync_StateOpen_ReturnsConnected()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK,
            """{"instance":{"state":"open","profileName":"+5511999990001"}}""");
        var result = await _svc.GetStatusAsync(CancellationToken.None);
        result.Status.Should().Be(WhatsAppConnectionStatus.Connected);
        result.PhoneNumber.Should().Be("+5511999990001");
    }

    [Fact]
    public async Task GetStatusAsync_StateClose_ReturnsDisconnected()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, """{"instance":{"state":"close"}}""");
        var result = await _svc.GetStatusAsync(CancellationToken.None);
        result.Status.Should().Be(WhatsAppConnectionStatus.Disconnected);
    }

    [Fact]
    public async Task GetStatusAsync_StateConnecting_ReturnsQrRequired()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, """{"instance":{"state":"connecting"}}""");
        var result = await _svc.GetStatusAsync(CancellationToken.None);
        result.Status.Should().Be(WhatsAppConnectionStatus.QrRequired);
    }

    [Fact]
    public async Task GetQrCodeAsync_ReturnsBase64()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, """{"base64":"data:image/png;base64,abc"}""");
        var result = await _svc.GetQrCodeAsync(CancellationToken.None);
        result.QrCode.Should().Be("data:image/png;base64,abc");
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage _response = new(HttpStatusCode.OK);
    public HttpRequestMessage? LastRequest { get; private set; }

    public void SetResponse(HttpStatusCode status, string json) =>
        _response = new HttpResponseMessage(status)
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(_response);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (class doesn't exist yet)**
```bash
cd backend
dotnet test tests/ImperadorBarberShop.UnitTests --filter "EvolutionApiWhatsAppServiceTests"
```
Expected: compile error — `EvolutionApiWhatsAppService` not found.

- [ ] **Step 3: Implement EvolutionApiWhatsAppService.cs**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/EvolutionApiWhatsAppService.cs
using System.Net.Http.Json;
using System.Text.Json;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;

namespace ImperadorBarberShop.Infrastructure.Services;

public class EvolutionApiWhatsAppService : IWhatsAppService
{
    private readonly HttpClient _http;
    private readonly IAppSettingsRepository _settings;

    public EvolutionApiWhatsAppService(HttpClient http, IAppSettingsRepository settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/message/sendText/{instance}");
        req.Headers.Add("apikey", apiKey);
        req.Content = JsonContent.Create(new { number = phone, text = message });
        (await _http.SendAsync(req, ct)).EnsureSuccessStatusCode();
    }

    public async Task<WhatsAppStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/instance/connectionState/{instance}");
        req.Headers.Add("apikey", apiKey);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var inst = doc.RootElement.GetProperty("instance");
        var state = inst.GetProperty("state").GetString();
        var phone = inst.TryGetProperty("profileName", out var p) ? p.GetString() : null;

        var status = state switch
        {
            "open"       => WhatsAppConnectionStatus.Connected,
            "connecting" => WhatsAppConnectionStatus.QrRequired,
            _            => WhatsAppConnectionStatus.Disconnected
        };
        return new WhatsAppStatus(status, phone);
    }

    public async Task<WhatsAppQr> GetQrCodeAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/instance/connect/{instance}");
        req.Headers.Add("apikey", apiKey);
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return new WhatsAppQr(doc.RootElement.GetProperty("base64").GetString()!);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/instance/logout/{instance}");
        req.Headers.Add("apikey", apiKey);
        (await _http.SendAsync(req, ct)).EnsureSuccessStatusCode();
    }

    private async Task<(string baseUrl, string apiKey, string instance)> GetConfigAsync(CancellationToken ct)
    {
        var baseUrl  = await _settings.GetAsync("whatsapp:evolutionApiUrl",  ct);
        var apiKey   = await _settings.GetAsync("whatsapp:evolutionApiKey",  ct);
        var instance = await _settings.GetAsync("whatsapp:instanceName",     ct);
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(instance))
            throw new InvalidOperationException(
                "WhatsApp not configured. Set whatsapp:evolutionApiUrl, whatsapp:evolutionApiKey, whatsapp:instanceName in AppSettings.");
        return (baseUrl.TrimEnd('/'), apiKey, instance);
    }
}
```

- [ ] **Step 4: Register in DependencyInjection.cs**

After `services.AddHttpClient();` add:
```csharp
        services.AddScoped<IWhatsAppService>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("evolution");
            var repo = sp.GetRequiredService<IAppSettingsRepository>();
            return new EvolutionApiWhatsAppService(http, repo);
        });
```

- [ ] **Step 5: Run tests — expect PASS**
```bash
cd backend
dotnet test tests/ImperadorBarberShop.UnitTests --filter "EvolutionApiWhatsAppServiceTests"
```
Expected: 5 passed.

- [ ] **Step 6: Commit**
```bash
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/EvolutionApiWhatsAppService.cs \
        backend/tests/ImperadorBarberShop.UnitTests/Services/EvolutionApiWhatsAppServiceTests.cs \
        backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs
git commit -m "feat(infra): EvolutionApiWhatsAppService + unit tests"
```

---

## Task 5: NotificationService + unit tests

**Files:**
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/NotificationService.cs`
- Create: `backend/tests/ImperadorBarberShop.UnitTests/Services/NotificationServiceTests.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`

**Consumes:** `IEmailService`, `IWhatsAppService`, `IAppSettingsRepository`, `INotificationService` (Task 2)

`channels` = CSV from `notifications:channels` split by `,`. WhatsApp barber notification goes to `whatsapp:notificationPhone` if set; if empty/null, only client gets WhatsApp. Time displayed as `scheduledAt.AddHours(-3)`.

- [ ] **Step 1: Write failing tests**

```csharp
// backend/tests/ImperadorBarberShop.UnitTests/Services/NotificationServiceTests.cs
using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class NotificationServiceTests
{
    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly IWhatsAppService _wa = Substitute.For<IWhatsAppService>();
    private readonly IAppSettingsRepository _settings = Substitute.For<IAppSettingsRepository>();
    private readonly NotificationService _svc;

    public NotificationServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FrontendUrl"] = "http://localhost:3000" })
            .Build();
        _svc = new NotificationService(_email, _wa, _settings, config);
    }

    private void SetChannels(string channels, string? barberPhone = null)
    {
        _settings.GetAsync("notifications:channels", Arg.Any<CancellationToken>()).Returns(channels);
        _settings.GetAsync("whatsapp:notificationPhone", Arg.Any<CancellationToken>()).Returns(barberPhone);
    }

    private static (Appointment appt, Barber barber, List<Service> services) Build()
    {
        var user = User.CreateBarber("Carlos", "carlos@test.com", "hash");
        var barber = Barber.Create(user.Id);
        var svc = Service.Create("Corte", "Desc", 30, 35m);
        var appt = Appointment.Create("João", "+5511999990000", barber.Id,
            DateTime.UtcNow.AddDays(1), 30, null, new[] { svc.Id });
        return (appt, barber, new List<Service> { svc });
    }

    [Fact]
    public async Task Created_EmailOnly_CallsEmailNotWhatsApp()
    {
        SetChannels("email");
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _email.Received(1).SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _wa.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Created_WhatsAppNoBarberPhone_SendsToClientOnly()
    {
        SetChannels("whatsapp", barberPhone: null);
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _wa.Received(1).SendAsync(appt.ClientPhone, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Created_WhatsAppWithBarberPhone_SendsTwice()
    {
        SetChannels("whatsapp", barberPhone: "+5511988880000");
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _wa.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Created_BothChannels_CallsBoth()
    {
        SetChannels("email,whatsapp");
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _email.Received(1).SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _wa.Received(1).SendAsync(appt.ClientPhone, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_WhatsApp_SendsCancelMessageToClient()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1), 30, null, Array.Empty<Guid>());
        await _svc.SendAppointmentCancelledAsync(appt, CancellationToken.None);
        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains("cancelado")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Completed_WhatsApp_SendsReviewLinkToClient()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1), 30, null, Array.Empty<Guid>());
        await _svc.SendAppointmentCompletedAsync(appt, CancellationToken.None);
        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains(appt.AccessToken)),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**
```bash
cd backend
dotnet test tests/ImperadorBarberShop.UnitTests --filter "NotificationServiceTests"
```
Expected: compile error — `NotificationService` not found.

- [ ] **Step 3: Implement NotificationService.cs**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/NotificationService.cs
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ImperadorBarberShop.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _email;
    private readonly IWhatsAppService _wa;
    private readonly IAppSettingsRepository _settings;
    private readonly string _frontendUrl;

    public NotificationService(
        IEmailService email,
        IWhatsAppService wa,
        IAppSettingsRepository settings,
        IConfiguration config)
    {
        _email      = email;
        _wa         = wa;
        _settings   = settings;
        _frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
    }

    public async Task SendAppointmentCreatedAsync(
        Appointment appointment, Barber barber, List<Service> services, CancellationToken ct = default)
    {
        var channels     = await GetChannelsAsync(ct);
        var serviceNames = string.Join(", ", services.Select(s => s.Name));
        var local        = appointment.ScheduledAt.AddHours(-3); // ponytail: hardcoded UTC-3 (Brasília)
        var date         = local.ToString("dd/MM/yyyy");
        var time         = local.ToString("HH:mm");

        if (channels.Contains("email") && barber.User is not null)
            await _email.SendAppointmentCreatedAsync(
                barber.User.Email, barber.User.Name,
                appointment.ClientName, appointment.ClientPhone,
                appointment.ScheduledAt, ct);

        if (channels.Contains("whatsapp"))
        {
            var barberPhone = await _settings.GetAsync("whatsapp:notificationPhone", ct);
            if (!string.IsNullOrEmpty(barberPhone))
            {
                var barberMsg = $"📅 Novo agendamento!\n{appointment.ClientName} marcou {serviceNames} " +
                                $"para {date} às {time}.\nTelefone: {appointment.ClientPhone}";
                await _wa.SendAsync(barberPhone, barberMsg, ct);
            }

            var clientMsg = $"✅ Agendamento confirmado!\n{serviceNames} com " +
                            $"{barber.User?.Name ?? "barbeiro"} em {date} às {time}.\n" +
                            $"Gerenciar: {_frontendUrl}/agendamento/{appointment.AccessToken}";
            await _wa.SendAsync(appointment.ClientPhone, clientMsg, ct);
        }
    }

    public async Task SendAppointmentCancelledAsync(Appointment appointment, CancellationToken ct = default)
    {
        var channels     = await GetChannelsAsync(ct);
        var serviceNames = string.Join(", ", appointment.AppointmentServices.Select(s => s.Service?.Name ?? ""));
        var barberName   = appointment.Barber?.User?.Name ?? "barbeiro";
        var local        = appointment.ScheduledAt.AddHours(-3);

        if (channels.Contains("whatsapp"))
        {
            var msg = $"❌ Agendamento cancelado.\n{serviceNames} com {barberName} em " +
                      $"{local:dd/MM/yyyy} às {local:HH:mm} foi cancelado.";
            await _wa.SendAsync(appointment.ClientPhone, msg, ct);
        }
    }

    public async Task SendAppointmentCompletedAsync(Appointment appointment, CancellationToken ct = default)
    {
        var channels = await GetChannelsAsync(ct);
        if (channels.Contains("whatsapp"))
        {
            var msg = $"⭐ Como foi? Deixe sua avaliação:\n{_frontendUrl}/agendamento/{appointment.AccessToken}";
            await _wa.SendAsync(appointment.ClientPhone, msg, ct);
        }
    }

    public async Task SendReminderAsync(Appointment appointment, CancellationToken ct = default)
    {
        var channels   = await GetChannelsAsync(ct);
        var barberName = appointment.Barber?.User?.Name ?? "barbeiro";
        var local      = appointment.ScheduledAt.AddHours(-3);

        if (channels.Contains("whatsapp"))
        {
            var msg = $"⏰ Lembrete: seu agendamento com {barberName} é hoje às {local:HH:mm}.\n" +
                      $"Gerenciar: {_frontendUrl}/agendamento/{appointment.AccessToken}";
            await _wa.SendAsync(appointment.ClientPhone, msg, ct);
        }
    }

    private async Task<HashSet<string>> GetChannelsAsync(CancellationToken ct)
    {
        var raw = await _settings.GetAsync("notifications:channels", ct) ?? "email";
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Register NotificationService in DependencyInjection.cs**

After the `IWhatsAppService` registration add:
```csharp
        services.AddScoped<INotificationService, NotificationService>();
```

- [ ] **Step 5: Run tests — expect PASS**
```bash
cd backend
dotnet test tests/ImperadorBarberShop.UnitTests --filter "NotificationServiceTests"
```
Expected: 6 passed.

- [ ] **Step 6: Commit**
```bash
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/NotificationService.cs \
        backend/tests/ImperadorBarberShop.UnitTests/Services/NotificationServiceTests.cs \
        backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs
git commit -m "feat(infra): NotificationService with channel dispatch + unit tests"
```

---

## Task 6: Update 4 appointment handlers to use INotificationService

**Files:**
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CreateAppointmentCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentByTokenCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CancelAppointmentByBarberCommand.cs`
- Modify: `backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/CompleteAppointmentCommand.cs`

**Consumes:** `INotificationService` (Task 2); existing handler patterns

**Rule:** Every handler wraps its notification call in `try/catch` — notification failures must not roll back the operation.

- [ ] **Step 1: Update CreateAppointmentCommandHandler**

Replace `IEmailService _emailService` field + constructor parameter with `INotificationService _notificationService`. Update the notification block after `SaveChangesAsync`:

Full handler after changes:
```csharp
public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, CreateAppointmentResult>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentCommandHandler(
        IBarberRepository barberRepository,
        IServiceRepository serviceRepository,
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _barberRepository      = barberRepository;
        _serviceRepository     = serviceRepository;
        _appointmentRepository = appointmentRepository;
        _notificationService   = notificationService;
        _unitOfWork            = unitOfWork;
    }

    public async Task<CreateAppointmentResult> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken);
        if (barber is null)
            throw new KeyNotFoundException($"Barber '{request.BarberId}' not found.");

        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count != request.ServiceIds.Count)
            throw new KeyNotFoundException("One or more services were not found.");

        var recentCount = await _appointmentRepository.CountCreatedByPhoneSinceAsync(
            request.ClientPhone, DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentCount >= 3)
            throw new InvalidOperationException("Too many appointment requests from this phone number. Try again later.");

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
            request.ClientName, request.ClientPhone, request.BarberId,
            request.ScheduledAt, totalDuration, request.Notes, request.ServiceIds);

        await _appointmentRepository.AddAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.SendAppointmentCreatedAsync(
                appointment, barber, services, cancellationToken);
        }
        catch { /* best-effort */ }

        return new CreateAppointmentResult(appointment.Id, appointment.AccessToken);
    }
}
```

Also remove the `using ImperadorBarberShop.Application.Interfaces;` import for `IEmailService` if it becomes unused (keep it if `INotificationService` is in the same namespace).

- [ ] **Step 2: Update CancelAppointmentByTokenCommandHandler**

Add `INotificationService` to constructor. After `SaveChangesAsync`, add:
```csharp
        try
        {
            await _notificationService.SendAppointmentCancelledAsync(appointment, cancellationToken);
        }
        catch { /* best-effort */ }
```

Full handler:
```csharp
public class CancelAppointmentByTokenCommandHandler : IRequestHandler<CancelAppointmentByTokenCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentByTokenCommandHandler(
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _notificationService   = notificationService;
        _unitOfWork            = unitOfWork;
    }

    public async Task Handle(CancelAppointmentByTokenCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        if (appointment.ScheduledAt - DateTime.UtcNow <= TimeSpan.FromHours(2))
            throw new InvalidOperationException("Cannot cancel an appointment within 2 hours of the scheduled time.");

        appointment.Cancel();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.SendAppointmentCancelledAsync(appointment, cancellationToken);
        }
        catch { /* best-effort */ }
    }
}
```

- [ ] **Step 3: Update CancelAppointmentByBarberCommandHandler**

Same pattern — add `INotificationService` + call `SendAppointmentCancelledAsync` after save:
```csharp
public class CancelAppointmentByBarberCommandHandler : IRequestHandler<CancelAppointmentByBarberCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentByBarberCommandHandler(
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _notificationService   = notificationService;
        _unitOfWork            = unitOfWork;
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

        try
        {
            await _notificationService.SendAppointmentCancelledAsync(appointment, cancellationToken);
        }
        catch { /* best-effort */ }
    }
}
```

- [ ] **Step 4: Update CompleteAppointmentCommandHandler**

Same pattern — add `INotificationService` + call `SendAppointmentCompletedAsync` after save:
```csharp
public class CompleteAppointmentCommandHandler : IRequestHandler<CompleteAppointmentCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _notificationService   = notificationService;
        _unitOfWork            = unitOfWork;
    }

    public async Task Handle(CompleteAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (appointment.BarberId != request.BarberId)
            throw new ForbiddenException("You are not authorized to complete this appointment.");

        appointment.Complete();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.SendAppointmentCompletedAsync(appointment, cancellationToken);
        }
        catch { /* best-effort */ }
    }
}
```

- [ ] **Step 5: Update existing unit tests for the 4 handlers**

The handlers now require `INotificationService`. Update these test constructors:
- `CreateAppointmentCommandHandlerTests.cs` — replace `IEmailService _emailService` with `INotificationService _notificationService = Substitute.For<INotificationService>();` and update constructor call
- `CancelAppointmentByTokenCommandHandlerTests.cs` — add `INotificationService _notificationService`
- `CancelAppointmentByBarberCommandHandlerTests.cs` — add `INotificationService _notificationService`
- `CompleteAppointmentCommandHandlerTests.cs` — add `INotificationService _notificationService`

For `CreateAppointmentCommandHandlerTests`, change:
```csharp
// REMOVE:
private readonly IEmailService _emailService = Substitute.For<IEmailService>();
// ADD:
private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
```
And update the `_handler = new CreateAppointmentCommandHandler(...)` line to pass `_notificationService` instead of `_emailService`.

For the other three tests, add to the field declarations:
```csharp
private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
```
And update the handler constructor calls to include `_notificationService` before `_unitOfWork`.

- [ ] **Step 6: Run all unit tests**
```bash
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests
```
Expected: all pass.

- [ ] **Step 7: Commit**
```bash
git add backend/src/Application/ImperadorBarberShop.Application/Commands/Appointments/ \
        backend/tests/ImperadorBarberShop.UnitTests/Appointments/
git commit -m "feat(app): appointment handlers use INotificationService"
```

---

## Task 7: ReminderBackgroundService + new repo method

**Files:**
- Modify: `backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppointmentRepository.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppointmentRepository.cs`
- Create: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/ReminderBackgroundService.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`

**Consumes:** `INotificationService`, `IAppSettingsRepository`, `IAppointmentRepository`, `IUnitOfWork`

`ReminderBackgroundService` is a singleton `BackgroundService`. It cannot directly inject scoped services — uses `IServiceScopeFactory` to create a scope per tick.

- [ ] **Step 1: Add GetPendingRemindersAsync to IAppointmentRepository**

Add to the interface:
```csharp
    Task<List<Appointment>> GetPendingRemindersAsync(DateTime windowEnd, CancellationToken ct = default);
```

- [ ] **Step 2: Implement in AppointmentRepository**

```csharp
public async Task<List<Appointment>> GetPendingRemindersAsync(DateTime windowEnd, CancellationToken ct = default)
    => await _context.Appointments
        .Include(a => a.Barber).ThenInclude(b => b.User)
        .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
        .Where(a => a.Status == AppointmentStatus.Accepted
            && a.ReminderSentAt == null
            && a.ScheduledAt > DateTime.UtcNow
            && a.ScheduledAt <= windowEnd)
        .ToListAsync(ct);
```

- [ ] **Step 3: Create ReminderBackgroundService.cs**

```csharp
// backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/ReminderBackgroundService.cs
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImperadorBarberShop.Infrastructure.Services;

public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessRemindersAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken ct)
    {
        using var scope           = _scopeFactory.CreateScope();
        var settingsRepo          = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
        var appointmentRepo       = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var notificationService   = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var unitOfWork            = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var minutesStr = await settingsRepo.GetAsync("notifications:reminderMinutesBefore", ct) ?? "60";
        var minutes    = int.TryParse(minutesStr, out var m) ? m : 60;
        var windowEnd  = DateTime.UtcNow.AddMinutes(minutes);

        var appointments = await appointmentRepo.GetPendingRemindersAsync(windowEnd, ct);

        foreach (var appointment in appointments)
        {
            try
            {
                await notificationService.SendReminderAsync(appointment, ct);
                appointment.MarkReminderSent();
                await appointmentRepo.UpdateAsync(appointment, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send reminder for appointment {Id}", appointment.Id);
            }
        }
    }
}
```

- [ ] **Step 4: Register in DependencyInjection.cs**

After the `INotificationService` registration add:
```csharp
        services.AddHostedService<ReminderBackgroundService>();
```

- [ ] **Step 5: Build and run unit tests to verify no regressions**
```bash
cd backend
dotnet build src/Api/ImperadorBarberShop.Api
dotnet test tests/ImperadorBarberShop.UnitTests
```
Expected: build succeeded, all unit tests pass.

- [ ] **Step 6: Commit**
```bash
git add backend/src/Domain/ImperadorBarberShop.Domain/Interfaces/IAppointmentRepository.cs \
        backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Repositories/AppointmentRepository.cs \
        backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Services/ReminderBackgroundService.cs \
        backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs
git commit -m "feat(infra): ReminderBackgroundService polls every 60s for appointment reminders"
```

---

## Task 8: Admin queries, commands + AdminController endpoints

**Files:**
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetWhatsAppStatusQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetWhatsAppQrQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetNotificationSettingsQuery.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/DisconnectWhatsAppCommand.cs`
- Create: `backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/UpdateNotificationSettingsCommand.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Controllers/AdminController.cs`

**Consumes:** `IWhatsAppService`, `IAppSettingsRepository`; existing `AdminController` pattern (thin, dispatches to MediatR)

- [ ] **Step 1: Create GetWhatsAppStatusQuery.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetWhatsAppStatusQuery.cs
using ImperadorBarberShop.Application.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetWhatsAppStatusQuery : IRequest<WhatsAppStatusDto>;

public record WhatsAppStatusDto(string Status, string? PhoneNumber);

public class GetWhatsAppStatusQueryHandler : IRequestHandler<GetWhatsAppStatusQuery, WhatsAppStatusDto>
{
    private readonly IWhatsAppService _whatsApp;

    public GetWhatsAppStatusQueryHandler(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    public async Task<WhatsAppStatusDto> Handle(GetWhatsAppStatusQuery request, CancellationToken ct)
    {
        try
        {
            var status = await _whatsApp.GetStatusAsync(ct);
            var statusStr = status.Status switch
            {
                WhatsAppConnectionStatus.Connected    => "connected",
                WhatsAppConnectionStatus.QrRequired   => "qr_required",
                _                                     => "disconnected"
            };
            return new WhatsAppStatusDto(statusStr, status.PhoneNumber);
        }
        catch
        {
            return new WhatsAppStatusDto("disconnected", null);
        }
    }
}
```

- [ ] **Step 2: Create GetWhatsAppQrQuery.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetWhatsAppQrQuery.cs
using ImperadorBarberShop.Application.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetWhatsAppQrQuery : IRequest<WhatsAppQrDto>;

public record WhatsAppQrDto(string QrCode);

public class GetWhatsAppQrQueryHandler : IRequestHandler<GetWhatsAppQrQuery, WhatsAppQrDto>
{
    private readonly IWhatsAppService _whatsApp;

    public GetWhatsAppQrQueryHandler(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    public async Task<WhatsAppQrDto> Handle(GetWhatsAppQrQuery request, CancellationToken ct)
    {
        var qr = await _whatsApp.GetQrCodeAsync(ct);
        return new WhatsAppQrDto(qr.QrCode);
    }
}
```

- [ ] **Step 3: Create GetNotificationSettingsQuery.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Queries/Admin/GetNotificationSettingsQuery.cs
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;

public record NotificationSettingsDto(
    List<string> Channels,
    int ReminderMinutesBefore,
    string? NotificationPhone);

public class GetNotificationSettingsQueryHandler : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private readonly IAppSettingsRepository _settings;

    public GetNotificationSettingsQueryHandler(IAppSettingsRepository settings) => _settings = settings;

    public async Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery request, CancellationToken ct)
    {
        var channelsRaw = await _settings.GetAsync("notifications:channels", ct) ?? "email";
        var minutesStr  = await _settings.GetAsync("notifications:reminderMinutesBefore", ct) ?? "60";
        var phone       = await _settings.GetAsync("whatsapp:notificationPhone", ct);
        var channels    = channelsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var minutes = int.TryParse(minutesStr, out var m) ? m : 60;
        return new NotificationSettingsDto(channels, minutes, phone);
    }
}
```

- [ ] **Step 4: Create DisconnectWhatsAppCommand.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/DisconnectWhatsAppCommand.cs
using ImperadorBarberShop.Application.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record DisconnectWhatsAppCommand : IRequest;

public class DisconnectWhatsAppCommandHandler : IRequestHandler<DisconnectWhatsAppCommand>
{
    private readonly IWhatsAppService _whatsApp;

    public DisconnectWhatsAppCommandHandler(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    public async Task Handle(DisconnectWhatsAppCommand request, CancellationToken ct)
        => await _whatsApp.DisconnectAsync(ct);
}
```

- [ ] **Step 5: Create UpdateNotificationSettingsCommand.cs**

```csharp
// backend/src/Application/ImperadorBarberShop.Application/Commands/Admin/UpdateNotificationSettingsCommand.cs
using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record UpdateNotificationSettingsCommand(
    List<string> Channels,
    int ReminderMinutesBefore,
    string? NotificationPhone) : IRequest;

public class UpdateNotificationSettingsCommandValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    public UpdateNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.Channels).NotEmpty();
        RuleFor(x => x.ReminderMinutesBefore).InclusiveBetween(5, 1440);
        RuleFor(x => x.NotificationPhone)
            .Matches(@"^\+55\d{11}$")
            .When(x => !string.IsNullOrEmpty(x.NotificationPhone))
            .WithMessage("NotificationPhone must be in the format +55DDDXXXXXXXXX.");
    }
}

public class UpdateNotificationSettingsCommandHandler : IRequestHandler<UpdateNotificationSettingsCommand>
{
    private readonly IAppSettingsRepository _settings;

    public UpdateNotificationSettingsCommandHandler(IAppSettingsRepository settings) => _settings = settings;

    public async Task Handle(UpdateNotificationSettingsCommand request, CancellationToken ct)
    {
        var validChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "whatsapp" };
        var filtered = request.Channels
            .Where(c => validChannels.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        await _settings.SetAsync("notifications:channels", string.Join(",", filtered), ct);
        await _settings.SetAsync("notifications:reminderMinutesBefore",
            request.ReminderMinutesBefore.ToString(), ct);
        if (request.NotificationPhone is not null)
            await _settings.SetAsync("whatsapp:notificationPhone", request.NotificationPhone, ct);
    }
}
```

- [ ] **Step 6: Add 5 endpoints to AdminController.cs**

Add these usings at the top:
```csharp
using ImperadorBarberShop.Application.Queries.Admin;
using ImperadorBarberShop.Application.Commands.Admin;
```

Add these methods before the private `GetImageValidationError` method:
```csharp
    // WhatsApp
    [HttpGet("whatsapp/status")]
    public async Task<IActionResult> GetWhatsAppStatus(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWhatsAppStatusQuery(), ct));

    [HttpGet("whatsapp/qr")]
    public async Task<IActionResult> GetWhatsAppQr(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWhatsAppQrQuery(), ct));

    [HttpPost("whatsapp/disconnect")]
    public async Task<IActionResult> DisconnectWhatsApp(CancellationToken ct)
    {
        await _mediator.Send(new DisconnectWhatsAppCommand(), ct);
        return NoContent();
    }

    // Notification settings
    [HttpGet("notifications/settings")]
    public async Task<IActionResult> GetNotificationSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetNotificationSettingsQuery(), ct));

    [HttpPut("notifications/settings")]
    public async Task<IActionResult> UpdateNotificationSettings(
        [FromBody] UpdateNotificationSettingsRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateNotificationSettingsCommand(
            request.Channels, request.ReminderMinutesBefore, request.NotificationPhone), ct);
        return NoContent();
    }
```

Add request record at the bottom of the file:
```csharp
public record UpdateNotificationSettingsRequest(
    List<string> Channels,
    int ReminderMinutesBefore,
    string? NotificationPhone);
```

- [ ] **Step 7: Update WebAppFixture.cs — add fake IWhatsAppService**

In `ConfigureWebHost`, after the `FakeImageService` registration block, add:
```csharp
            // Replace real WhatsApp service with a no-op fake for integration tests
            var waDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWhatsAppService));
            if (waDescriptor is not null) services.Remove(waDescriptor);
            services.AddScoped<IWhatsAppService, FakeWhatsAppService>();
```

Add at the bottom of the file (after `FakeImageService`):
```csharp
public class FakeWhatsAppService : IWhatsAppService
{
    public Task SendAsync(string phone, string message, CancellationToken ct = default) => Task.CompletedTask;
    public Task<WhatsAppStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new WhatsAppStatus(WhatsAppConnectionStatus.Disconnected, null));
    public Task<WhatsAppQr> GetQrCodeAsync(CancellationToken ct = default)
        => Task.FromResult(new WhatsAppQr("data:image/png;base64,fake"));
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
}
```

Also add to `TestEnvironmentVariables`: `["FrontendUrl"] = "http://localhost:3000"`.

- [ ] **Step 8: Build and run all tests**
```bash
cd backend
dotnet build src/Api/ImperadorBarberShop.Api
dotnet test
```
Expected: build succeeded, all tests pass.

- [ ] **Step 9: Commit**
```bash
git add backend/src/Application/ backend/src/Api/ backend/tests/ImperadorBarberShop.IntegrationTests/WebAppFixture.cs
git commit -m "feat(api): WhatsApp + notification settings admin endpoints"
```

---

## Task 9: Frontend — types, API client, hooks

**Files:**
- Modify: `frontend/src/types/api.types.ts`
- Create: `frontend/src/lib/api/whatsapp.api.ts`
- Create: `frontend/src/hooks/useWhatsApp.ts`

**Consumes:** `GET /admin/whatsapp/status`, `GET /admin/whatsapp/qr`, `POST /admin/whatsapp/disconnect`, `GET /admin/notifications/settings`, `PUT /admin/notifications/settings`

- [ ] **Step 1: Add types to api.types.ts**

Add at the end of `frontend/src/types/api.types.ts`:
```typescript
export type WhatsAppConnectionStatus = 'connected' | 'disconnected' | 'qr_required'

export interface WhatsAppStatus {
  status: WhatsAppConnectionStatus
  phoneNumber?: string | null
}

export interface WhatsAppQr {
  qrCode: string
}

export interface NotificationSettings {
  channels: string[]
  reminderMinutesBefore: number
  notificationPhone?: string | null
}

export interface UpdateNotificationSettingsPayload {
  channels: string[]
  reminderMinutesBefore: number
  notificationPhone?: string | null
}
```

- [ ] **Step 2: Create whatsapp.api.ts**

```typescript
// frontend/src/lib/api/whatsapp.api.ts
import apiClient from './client'
import type { WhatsAppQr, WhatsAppStatus, NotificationSettings, UpdateNotificationSettingsPayload } from '@/types/api.types'

export const whatsappApi = {
  getStatus: () =>
    apiClient.get<WhatsAppStatus>('/admin/whatsapp/status').then((r) => r.data),

  getQr: () =>
    apiClient.get<WhatsAppQr>('/admin/whatsapp/qr').then((r) => r.data),

  disconnect: () =>
    apiClient.post('/admin/whatsapp/disconnect'),

  getNotificationSettings: () =>
    apiClient.get<NotificationSettings>('/admin/notifications/settings').then((r) => r.data),

  updateNotificationSettings: (payload: UpdateNotificationSettingsPayload) =>
    apiClient.put('/admin/notifications/settings', payload),
}
```

- [ ] **Step 3: Create useWhatsApp.ts**

```typescript
// frontend/src/hooks/useWhatsApp.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { whatsappApi } from '@/lib/api/whatsapp.api'
import type { UpdateNotificationSettingsPayload } from '@/types/api.types'

export function useWhatsAppStatus(refetchInterval?: number) {
  return useQuery({
    queryKey: ['admin', 'whatsapp', 'status'],
    queryFn: whatsappApi.getStatus,
    refetchInterval: refetchInterval ?? 10_000,
  })
}

export function useWhatsAppQr() {
  return useQuery({
    queryKey: ['admin', 'whatsapp', 'qr'],
    queryFn: whatsappApi.getQr,
    refetchInterval: 5_000,
  })
}

export function useDisconnectWhatsApp() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: whatsappApi.disconnect,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'whatsapp', 'status'] }),
  })
}

export function useNotificationSettings() {
  return useQuery({
    queryKey: ['admin', 'notifications', 'settings'],
    queryFn: whatsappApi.getNotificationSettings,
  })
}

export function useUpdateNotificationSettings() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateNotificationSettingsPayload) =>
      whatsappApi.updateNotificationSettings(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'notifications', 'settings'] }),
  })
}
```

- [ ] **Step 4: Commit**
```bash
git add frontend/src/types/api.types.ts \
        frontend/src/lib/api/whatsapp.api.ts \
        frontend/src/hooks/useWhatsApp.ts
git commit -m "feat(frontend): WhatsApp API client and TanStack Query hooks"
```

---

## Task 10: Frontend — /admin/whatsapp page + nav link + component tests

**Files:**
- Create: `frontend/src/app/admin/whatsapp/page.tsx`
- Modify: `frontend/src/app/admin/layout.tsx`
- Create: `frontend/tests/unit/app/admin/WhatsAppPage.test.tsx`

**Consumes:** hooks from Task 9; `brand-*` Tailwind tokens; existing admin page patterns (e.g. `DashboardPage`)

The page has two tabs: **Conexão** and **Notificações**.

**Conexão tab:**
- Shows status badge: green "Conectado (número)", yellow "Aguardando QR", red "Desconectado"
- When `status === 'qr_required'`: renders `<img src={qrCode} alt="QR Code" />` and polls `useWhatsAppQr` every 5s; also polls status every 5s until connected
- When `status === 'connected'`: shows "Desconectar" button

**Notificações tab:**
- Checkboxes: `[ ] E-mail` `[ ] WhatsApp`
- Number input: "Lembrete (minutos antes)" min=5 max=1440
- Text input: "Telefone de notificação dos barbeiros (opcional)" placeholder="+5511999990000"
- Submit button: "Salvar"

- [ ] **Step 1: Create /admin/whatsapp/page.tsx**

```tsx
// frontend/src/app/admin/whatsapp/page.tsx
'use client'

import { useState } from 'react'
import {
  useWhatsAppStatus,
  useWhatsAppQr,
  useDisconnectWhatsApp,
  useNotificationSettings,
  useUpdateNotificationSettings,
} from '@/hooks/useWhatsApp'

type Tab = 'connection' | 'notifications'

export default function WhatsAppPage() {
  const [tab, setTab] = useState<Tab>('connection')

  return (
    <div>
      <h1 className="font-montserrat text-2xl font-bold text-brand-gold mb-6">WhatsApp</h1>

      <div className="flex gap-2 mb-6 border-b border-brand-white/10">
        {(['connection', 'notifications'] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              tab === t
                ? 'border-b-2 border-brand-gold text-brand-gold'
                : 'text-brand-white/60 hover:text-brand-white'
            }`}
          >
            {t === 'connection' ? 'Conexão' : 'Notificações'}
          </button>
        ))}
      </div>

      {tab === 'connection' ? <ConnectionTab /> : <NotificationsTab />}
    </div>
  )
}

function ConnectionTab() {
  const { data: status, isLoading } = useWhatsAppStatus(
    // poll faster when waiting for QR
    undefined
  )
  const { data: qr } = useWhatsAppQr()
  const disconnect = useDisconnectWhatsApp()

  if (isLoading) return <p className="text-brand-white/50">Verificando conexão...</p>

  const statusLabel = {
    connected:    { text: 'Conectado', color: 'text-green-400' },
    qr_required:  { text: 'Aguardando QR Code', color: 'text-yellow-400' },
    disconnected: { text: 'Desconectado', color: 'text-red-400' },
  }[status?.status ?? 'disconnected']

  return (
    <div className="flex flex-col gap-6 max-w-md">
      <div className="bg-brand-black-soft rounded-lg p-4 flex items-center gap-3">
        <span className={`font-semibold ${statusLabel.color}`}>{statusLabel.text}</span>
        {status?.phoneNumber && (
          <span className="text-brand-white/60 text-sm">{status.phoneNumber}</span>
        )}
      </div>

      {status?.status === 'qr_required' && qr && (
        <div className="flex flex-col items-center gap-3">
          <p className="text-brand-white/70 text-sm">
            Abra o WhatsApp no celular → Dispositivos vinculados → Vincular um dispositivo
          </p>
          <img
            src={qr.qrCode}
            alt="QR Code WhatsApp"
            className="w-64 h-64 rounded-lg"
          />
        </div>
      )}

      {status?.status === 'connected' && (
        <button
          onClick={() => disconnect.mutate()}
          disabled={disconnect.isPending}
          className="px-4 py-2 rounded-lg bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors text-sm w-fit"
        >
          {disconnect.isPending ? 'Desconectando...' : 'Desconectar'}
        </button>
      )}
    </div>
  )
}

function NotificationsTab() {
  const { data: settings } = useNotificationSettings()
  const update = useUpdateNotificationSettings()
  const [channels, setChannels] = useState<string[]>(settings?.channels ?? ['email', 'whatsapp'])
  const [minutes, setMinutes] = useState<number>(settings?.reminderMinutesBefore ?? 60)
  const [phone, setPhone] = useState<string>(settings?.notificationPhone ?? '')
  const [saved, setSaved] = useState(false)

  // sync state when settings load
  if (settings && channels.length === 0 && settings.channels.length > 0) {
    setChannels(settings.channels)
    setMinutes(settings.reminderMinutesBefore)
    setPhone(settings.notificationPhone ?? '')
  }

  const toggleChannel = (ch: string) =>
    setChannels((prev) =>
      prev.includes(ch) ? prev.filter((c) => c !== ch) : [...prev, ch]
    )

  const handleSave = () => {
    update.mutate(
      { channels, reminderMinutesBefore: minutes, notificationPhone: phone || null },
      {
        onSuccess: () => {
          setSaved(true)
          setTimeout(() => setSaved(false), 3000)
        },
      }
    )
  }

  return (
    <div className="flex flex-col gap-6 max-w-md">
      <div className="bg-brand-black-soft rounded-lg p-4 flex flex-col gap-3">
        <p className="text-brand-white/70 text-sm font-medium">Canais de notificação</p>
        {(['email', 'whatsapp'] as const).map((ch) => (
          <label key={ch} className="flex items-center gap-3 cursor-pointer">
            <input
              type="checkbox"
              checked={channels.includes(ch)}
              onChange={() => toggleChannel(ch)}
              className="accent-brand-gold w-4 h-4"
            />
            <span className="text-brand-white capitalize">{ch === 'email' ? 'E-mail' : 'WhatsApp'}</span>
          </label>
        ))}
      </div>

      <div className="bg-brand-black-soft rounded-lg p-4 flex flex-col gap-3">
        <label className="flex flex-col gap-1">
          <span className="text-brand-white/70 text-sm">Lembrete (minutos antes)</span>
          <input
            type="number"
            min={5}
            max={1440}
            value={minutes}
            onChange={(e) => setMinutes(Number(e.target.value))}
            className="bg-brand-black border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 w-32 focus:outline-none focus:border-brand-gold"
          />
        </label>

        <label className="flex flex-col gap-1">
          <span className="text-brand-white/70 text-sm">
            Telefone de notificação dos barbeiros (opcional)
          </span>
          <input
            type="text"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="+5511999990000"
            className="bg-brand-black border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 focus:outline-none focus:border-brand-gold"
          />
        </label>
      </div>

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={update.isPending}
          className="px-6 py-2 bg-brand-gold text-brand-black font-semibold rounded-lg hover:bg-brand-gold-light transition-colors disabled:opacity-50"
        >
          {update.isPending ? 'Salvando...' : 'Salvar'}
        </button>
        {saved && <span className="text-green-400 text-sm">Configurações salvas!</span>}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Add WhatsApp link to admin layout**

In `frontend/src/app/admin/layout.tsx`, add `{ href: '/admin/whatsapp', label: 'WhatsApp' }` to the nav array:

```tsx
          {[
            { href: '/admin/dashboard', label: 'Dashboard' },
            { href: '/admin/barbers', label: 'Barbeiros' },
            { href: '/admin/services', label: 'Serviços' },
            { href: '/admin/whatsapp', label: 'WhatsApp' },
          ].map(({ href, label }) => (
```

- [ ] **Step 3: Check MSW mock server setup**

Look at `frontend/tests/mocks/server.ts` (already exists, used by DashboardPage.test.tsx) to understand the mock server pattern. Use the same `server.use(http.get(...), ...)` pattern in the new tests.

- [ ] **Step 4: Create WhatsAppPage.test.tsx**

```tsx
// frontend/tests/unit/app/admin/WhatsAppPage.test.tsx
import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import WhatsAppPage from '@/app/admin/whatsapp/page'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('WhatsAppPage — Conexão tab', () => {
  beforeEach(() => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'connected', phoneNumber: '+5511999990001' })
      ),
      http.get('*/admin/whatsapp/qr', () =>
        HttpResponse.json({ qrCode: 'data:image/png;base64,fake' })
      ),
      http.get('*/admin/notifications/settings', () =>
        HttpResponse.json({ channels: ['email', 'whatsapp'], reminderMinutesBefore: 60, notificationPhone: null })
      )
    )
  })

  it('shows connected status badge', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByText('Conectado')).toBeInTheDocument()
    expect(screen.getByText('+5511999990001')).toBeInTheDocument()
  })

  it('shows QR code when status is qr_required', async () => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'qr_required', phoneNumber: null })
      )
    )
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByAltText('QR Code WhatsApp')).toBeInTheDocument()
  })

  it('shows disconnect button when connected', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByRole('button', { name: /desconectar/i })).toBeInTheDocument()
  })
})

describe('WhatsAppPage — Notificações tab', () => {
  beforeEach(() => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'disconnected', phoneNumber: null })
      ),
      http.get('*/admin/whatsapp/qr', () =>
        HttpResponse.json({ qrCode: 'data:image/png;base64,fake' })
      ),
      http.get('*/admin/notifications/settings', () =>
        HttpResponse.json({ channels: ['email', 'whatsapp'], reminderMinutesBefore: 60, notificationPhone: null })
      )
    )
  })

  it('renders notification settings tab and save button', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    const tab = screen.getByRole('button', { name: 'Notificações' })
    tab.click()
    expect(await screen.findByRole('button', { name: /salvar/i })).toBeInTheDocument()
    expect(screen.getByPlaceholderText('+5511999990000')).toBeInTheDocument()
  })
})
```

- [ ] **Step 5: Run frontend tests**
```bash
cd frontend && npm test -- --reporter=verbose
```
Expected: new WhatsApp tests pass, no regressions.

- [ ] **Step 6: Run integration tests to verify the full backend**
```bash
cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests
```
Expected: all pass.

- [ ] **Step 7: Commit**
```bash
git add frontend/src/app/admin/whatsapp/ \
        frontend/src/app/admin/layout.tsx \
        frontend/tests/unit/app/admin/WhatsAppPage.test.tsx
git commit -m "feat(frontend): /admin/whatsapp page — connection QR + notification settings"
```

---

## Self-Review Checklist

Before starting implementation, verify:

- [ ] All 5 notification events covered: created→barber, created→client, cancelled→client, completed→client, reminder→client ✓
- [ ] `IEmailService` preserved — `NotificationService` calls it when `email` channel active ✓
- [ ] `ReminderSentAt` prevents duplicate reminders across restarts ✓
- [ ] `best-effort` pattern: all 4 handlers wrap notification call in `try/catch` ✓
- [ ] `FakeWhatsAppService` in `WebAppFixture` prevents integration tests from hitting Evolution API ✓
- [ ] Bootstrap only seeds AppSettings keys that don't already exist — safe for restarts ✓
- [ ] `UpdateNotificationSettingsCommand` validates phone format and reminder range ✓
