# SQLite Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trocar o provider de banco de dados de PostgreSQL (Npgsql) para SQLite, eliminando Docker do dev local e qualquer serviço externo de banco em produção.

**Architecture:** O EF Core abstrai o provider — só trocamos o pacote e as chamadas `.UseNpgsql()` por `.UseSqlite()`. As migrations existentes são Postgres-específicas e precisam ser deletadas e recriadas. Os testes de integração trocam Testcontainers (Docker) por SQLite in-memory.

**Tech Stack:** .NET 9, EF Core 9, `Microsoft.EntityFrameworkCore.Sqlite` 9.0.6, SQLite 3, xUnit, `Microsoft.Data.Sqlite`

## Global Constraints

- EF Core versão `9.0.x` — manter alinhado com os outros pacotes EF Core já no projeto (atualmente `9.0.3`). Usar `9.0.6` para o pacote SQLite.
- Não modificar nenhum arquivo de Domínio (`Domain/`) nem Aplicação (`Application/`) — só Infrastructure, Api, e testes.
- Não alterar nenhuma entidade, repositório, ou handler existente.
- A connection string de dev vive em `appsettings.Development.json` (gitignored) — o plano instrui explicitamente o implementador a atualizar o arquivo local.
- WAL mode habilitado na inicialização via `PRAGMA journal_mode=WAL` — seguro e sem efeito para `:memory:` (SQLite retorna "memory" mas não lança exceção).

---

### Task 1: Trocar provider EF Core e limpar configs Postgres-específicas

**Files:**
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/ImperadorBarberShop.Infrastructure.csproj`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContextFactory.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/BarberConfiguration.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`
- Modify: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ServiceConfiguration.cs`
- Modify: `backend/src/Api/ImperadorBarberShop.Api/Program.cs`

**Interfaces:**
- Produces: projeto `Infrastructure` compilando com SQLite provider; `Program.cs` com WAL mode após migrate

- [ ] **Step 1: Trocar pacote NuGet no projeto Infrastructure**

Em `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/ImperadorBarberShop.Infrastructure.csproj`, substituir:

```xml
<!-- Remover esta linha: -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />

<!-- Adicionar no lugar: -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
```

- [ ] **Step 2: Atualizar DependencyInjection.cs**

Em `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs`, trocar o using e a chamada:

```csharp
// Remover o using de Npgsql se existir (normalmente é resolvido automaticamente)
// Linha 21 — trocar:
options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
// Por:
options.UseSqlite(configuration.GetConnectionString("DefaultConnection"))
```

O arquivo completo após a mudança (só a linha 21 muda):

```csharp
// Database
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
```

- [ ] **Step 3: Atualizar AppDbContextFactory.cs**

Em `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContextFactory.cs`, mudar a linha 23 (fallback) e linha 26:

```csharp
// Linha 23 — trocar o fallback:
?? "Data Source=imperador_barber.db";

// Linha 26 — trocar:
optionsBuilder.UseSqlite(connectionString);
```

Arquivo completo após mudança:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ImperadorBarberShop.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=imperador_barber.db";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
```

- [ ] **Step 4: Remover HasColumnType Postgres-específico das 3 configurações de entidade**

**`BarberConfiguration.cs`** — remover `.HasColumnType("decimal(3,2)")`:

```csharp
builder.Property(b => b.AverageRating)
    .IsRequired();
```

**`ExpenseConfiguration.cs`** — remover `.HasColumnType("numeric(10,2)")`:

```csharp
builder.Property(e => e.Amount).IsRequired();
```

**`ServiceConfiguration.cs`** — remover `.HasColumnType("decimal(10,2)")`:

```csharp
builder.Property(s => s.Price)
    .IsRequired();
```

- [ ] **Step 5: Adicionar WAL mode em Program.cs**

Em `backend/src/Api/ImperadorBarberShop.Api/Program.cs`, após `await db.Database.MigrateAsync()` (linha ~122), adicionar:

```csharp
// Enable WAL mode for SQLite: allows concurrent reads during writes
await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
```

O bloco completo fica assim:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    var seeder = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
    await seeder.SeedAsync();
}
```

- [ ] **Step 6: Verificar que compila**

Rodar a partir de `backend/`:

```bash
dotnet build
```

Expected: `Build succeeded.` sem erros (warnings de MailKit OK, são sobre vulnerabilidade conhecida do pacote, não do nosso código).

- [ ] **Step 7: Commit**

```bash
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/ImperadorBarberShop.Infrastructure.csproj
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/DependencyInjection.cs
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/AppDbContextFactory.cs
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/BarberConfiguration.cs
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Persistence/Configurations/ServiceConfiguration.cs
git add backend/src/Api/ImperadorBarberShop.Api/Program.cs
git commit -m "chore(infra): swap Npgsql for SQLite provider"
```

---

### Task 2: Deletar migrations antigas, regenerar, e verificar startup

**Files:**
- Delete: `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/` (todos os arquivos)
- Create: nova migration `InitialCreate` (gerada por `dotnet ef`)
- Modify: `backend/src/Api/ImperadorBarberShop.Api/appsettings.Development.json` (connection string — arquivo local gitignored)
- Modify: `.gitignore` (adicionar `*.db`, `*.db-shm`, `*.db-wal`)
- Delete: `docker-compose.yml`
- Modify: `backend/CLAUDE.md` (atualizar tech stack e instruções)

**Interfaces:**
- Consumes: provider SQLite registrado da Task 1
- Produces: migration `InitialCreate` SQLite-compatível; app iniciando sem erros; `*.db` no gitignore

- [ ] **Step 1: Deletar todas as migrations existentes**

As migrations atuais são geradas para Postgres e incompatíveis com SQLite. Deletar do diretório:

```bash
cd backend
rm -rf src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/
```

Ou manualmente deletar todos os arquivos em `backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/`. O diretório em si pode ser removido — `dotnet ef` o recria.

- [ ] **Step 2: Regenerar migration com SQLite**

Rodar a partir de `backend/`:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```

Expected: mensagem `Done. To undo this action, use 'ef migrations remove'`. Confira que o arquivo `Migrations/YYYYMMDDHHMMSS_InitialCreate.cs` foi criado e **não contém** `NpgsqlModelBuilderExtensions` nem `UseIdentityByDefaultColumns` — esses são Postgres-específicos e não devem aparecer.

- [ ] **Step 3: Atualizar a connection string local**

Editar manualmente o arquivo **`backend/src/Api/ImperadorBarberShop.Api/appsettings.Development.json`** (gitignored, arquivo local):

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=imperador_barber.db"
}
```

O arquivo completo atualizado:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=imperador_barber.db"
  },
  "Jwt": {
    "Secret": "imperador-barber-shop-dev-secret-key-32bytes!",
    "Issuer": "ImperadorBarberShop",
    "Audience": "ImperadorBarberShopFrontend",
    "ExpirationMinutes": 15
  },
  "Email": {
    "SmtpHost": "smtp.mailtrap.io",
    "SmtpPort": 587,
    "Username": "MAILTRAP_USERNAME",
    "Password": "MAILTRAP_PASSWORD",
    "FromAddress": "noreply@imperadorbarber.com",
    "FromName": "O Imperador Barber Shop"
  },
  "FrontendUrl": "http://localhost:3000",
  "Admin": {
    "Email": "admin@imperadorbarber.com",
    "Password": "Admin@123456"
  },
  "Cloudinary": {
    "CloudName": "dev-placeholder",
    "ApiKey": "000000000000000",
    "ApiSecret": "dev-placeholder-secret"
  }
}
```

- [ ] **Step 4: Adicionar *.db ao .gitignore**

No arquivo `.gitignore` na raiz do repositório, adicionar ao final:

```
# SQLite database files
*.db
*.db-shm
*.db-wal
```

- [ ] **Step 5: Deletar docker-compose.yml**

```bash
rm docker-compose.yml
```

- [ ] **Step 6: Atualizar backend/CLAUDE.md**

Em `backend/CLAUDE.md`, atualizar:

**Tech Stack** — trocar `EF Core 9 + Npgsql (PostgreSQL 16)` por `EF Core 9 + SQLite`:

```markdown
## Tech Stack
- .NET 9, ASP.NET Core, EF Core 9 + SQLite
- MediatR (CQRS), FluentValidation, AutoMapper
- BCrypt.Net-Next (password hashing, cost 12)
- JWT Bearer (access token 15 min, refresh token 7 days)
- MailKit (SMTP email)
```

**Running the API** — remover a linha `# Prerequisites: docker-compose up -d (starts PostgreSQL)`:

```markdown
## Running the API

```bash
# Ensure appsettings.Development.json has connection string + JWT settings (see root CLAUDE.md)

cd backend
dotnet run --project src/Api/ImperadorBarberShop.Api

# API: http://localhost:5044
# Swagger: http://localhost:5044/swagger
# Database file: backend/imperador_barber.db (created automatically on first run)
```

**Running Tests** — remover a linha que menciona Docker em testes de integração:

```markdown
## Running Tests

```bash
cd backend

# Unit tests (fast, no DB)
dotnet test tests/ImperadorBarberShop.UnitTests

# Integration tests (SQLite in-memory — no Docker required)
dotnet test tests/ImperadorBarberShop.IntegrationTests

# All tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

- [ ] **Step 7: Testar startup**

Rodar a partir de `backend/`:

```bash
dotnet run --project src/Api/ImperadorBarberShop.Api
```

Expected:
- Sem `failed to connect to docker` ou erros de Npgsql
- Log mostrando `Now listening on: http://localhost:5044`
- Arquivo `imperador_barber.db` criado no diretório `backend/src/Api/ImperadorBarberShop.Api/`

Testar o endpoint público:

```bash
curl http://localhost:5044/api/v1/services
```

Expected: JSON com os 6 serviços (Corte, Fade, Barba, Sobrancelha, Hidratação, Pigmentação). **Não deve retornar 500**.

- [ ] **Step 8: Commit**

```bash
git add backend/src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/
git add .gitignore
git add backend/CLAUDE.md
git commit -m "chore(infra): regenerate migrations for SQLite, drop docker-compose"
```

(O `docker-compose.yml` deletado e `appsettings.Development.json` modificado são tratados separadamente — o primeiro via `git rm docker-compose.yml`, o segundo é gitignored.)

```bash
git rm docker-compose.yml
git commit -m "chore: remove docker-compose (PostgreSQL no longer needed)"
```

---

### Task 3: Atualizar testes de integração para SQLite in-memory

**Files:**
- Modify: `backend/tests/ImperadorBarberShop.IntegrationTests/ImperadorBarberShop.IntegrationTests.csproj`
- Modify: `backend/tests/ImperadorBarberShop.IntegrationTests/WebAppFixture.cs`

**Interfaces:**
- Consumes: provider SQLite da Task 1; migrations SQLite da Task 2
- Produces: testes de integração rodando sem Docker, usando SQLite `:memory:`

**Contexto:** `WebAppFixture` atual usa `PostgreSqlContainer` (Testcontainers) que exige Docker. A nova versão usa `SqliteConnection` aberta em RAM. Uma instância de `SqliteConnection` aberta é passada para o `DbContext` — isso mantém o banco em memória vivo durante toda a execução dos testes. Se a conexão fechar, o banco some.

- [ ] **Step 1: Trocar pacote no .csproj dos testes**

Em `backend/tests/ImperadorBarberShop.IntegrationTests/ImperadorBarberShop.IntegrationTests.csproj`:

```xml
<!-- Remover: -->
<PackageReference Include="Testcontainers.PostgreSql" Version="4.4.0" />

<!-- Adicionar: -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.6" />
```

O arquivo completo após a mudança:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.3" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.6" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Api\ImperadorBarberShop.Api\ImperadorBarberShop.Api.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Reescrever WebAppFixture.cs**

Substituir o conteúdo de `backend/tests/ImperadorBarberShop.IntegrationTests/WebAppFixture.cs` pelo seguinte:

```csharp
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImperadorBarberShop.IntegrationTests;

public class WebAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Keep the connection open for the lifetime of the fixture so the in-memory
    // database survives across requests. Closing it drops the database.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    // appsettings.json (checked in) has no Jwt/Email section, and there is no
    // appsettings.Testing.json — ASP.NET Core's config chain for the "Testing"
    // environment never loads appsettings.Development.json (gitignored, local-only).
    // Without this, Program.cs throws InvalidOperationException("JWT settings are
    // not configured.") while building WebApplication.CreateBuilder(args) itself.
    //
    // This can't be fixed via ConfigureWebHost/ConfigureAppConfiguration: Program.cs
    // uses the minimal hosting model, and `builder.Configuration.GetSection("Jwt")...`
    // is read directly in top-level statements (Program.cs:56), BEFORE builder.Build()
    // is called (Program.cs:104). WebApplicationFactory's ConfigureWebHost hooks only
    // get applied to the IHostBuilder that wraps an already-running Main() via
    // HostFactoryResolver — by the time they'd run, line 56 has already executed and
    // thrown. Environment variables are read by WebApplication.CreateBuilder's default
    // configuration providers at construction time, so they're visible at line 56.
    private static readonly IReadOnlyDictionary<string, string> TestEnvironmentVariables = new Dictionary<string, string>
    {
        ["Jwt__Secret"] = "integration-test-secret-key-at-least-32-bytes-long!!",
        ["Jwt__Issuer"] = "ImperadorBarberShop",
        ["Jwt__Audience"] = "ImperadorBarberShopFrontend",
        ["Jwt__ExpirationMinutes"] = "15",
        ["Email__SmtpHost"] = "localhost",
        ["Email__SmtpPort"] = "2525",
        ["Email__Username"] = "test",
        ["Email__Password"] = "test",
        ["Email__FromAddress"] = "noreply@test.com",
        ["Email__FromName"] = "Test",
        ["Admin__Email"] = "admin@test.com",
        ["Admin__Password"] = "AdminTest123!"
    };

    public async Task InitializeAsync()
    {
        foreach (var (key, value) in TestEnvironmentVariables)
            Environment.SetEnvironmentVariable(key, value);

        await _connection.OpenAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.CloseAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the existing AppDbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Register with the shared in-memory SQLite connection
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // Replace real Cloudinary with a no-op fake for integration tests
            var imageDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IImageService));
            if (imageDescriptor is not null)
                services.Remove(imageDescriptor);

            services.AddScoped<IImageService, FakeImageService>();

            // Replace real WhatsApp service with a no-op fake for integration tests
            var waDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IWhatsAppService));
            if (waDescriptor is not null) services.Remove(waDescriptor);
            services.AddScoped<IWhatsAppService, FakeWhatsAppService>();

            // Apply migrations
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    /// <summary>
    /// Seeds a barber directly via MediatR (bypasses HTTP — use in tests that need a barber
    /// but the POST /auth/register/barber endpoint no longer exists).
    /// </summary>
    public async Task SeedBarberAsync(string name, string email, string password = "Password123!")
    {
        using var scope = Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new RegisterBarberCommand(name, email, password, []));
    }

    public HttpClient CreateAuthenticatedClient(string role, Guid userId, Guid? barberId = null)
    {
        // For integration tests, we generate a real JWT token
        using var scope = Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<ImperadorBarberShop.Application.Interfaces.IJwtService>();

        var user = role switch
        {
            "Barber" => ImperadorBarberShop.Domain.Entities.User.CreateBarber("Test Barber", $"barber-{userId}@test.com", "hash"),
            "Admin" => ImperadorBarberShop.Domain.Entities.User.CreateAdmin("Test Admin", $"admin-{userId}@test.com", "hash"),
            _ => ImperadorBarberShop.Domain.Entities.User.CreateClient("Test Client", $"client-{userId}@test.com", "hash")
        };

        var token = jwtService.GenerateAccessToken(user, barberId);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public class FakeImageService : IImageService
{
    public Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
        => Task.FromResult($"https://fake-cloudinary.com/{Guid.NewGuid()}/{fileName}");
}

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

- [ ] **Step 3: Rodar os testes**

```bash
cd backend
dotnet test tests/ImperadorBarberShop.IntegrationTests --verbosity normal
```

Expected: todos os testes passam. Sem erros de conexão com Docker, sem `PostgreSqlContainer`. Os testes devem completar mais rápido que antes (sem pull de imagem Docker, sem container startup).

Se algum teste falhar com erro de SQLite específico (ex: tipo não suportado), verificar se é um tipo `DateOnly`/`TimeOnly` — o EF Core 8+ converte automaticamente, mas pode precisar de um `ConfigureConventions` no `AppDbContext`. O erro mais comum seria algo como `"No value converter registered for type 'DateOnly'"` — neste caso, adicionar em `AppDbContext.OnModelCreating`:

```csharp
// Apenas se necessário — EF Core 9 deve tratar automaticamente
modelBuilder.UseValueConverterForType<DateOnly>(new DateOnlyConverter());
modelBuilder.UseValueConverterForType<TimeOnly>(new TimeOnlyConverter());
```

Sendo que esses converters existem no namespace `Microsoft.EntityFrameworkCore.Storage.ValueConversion`. Na prática, EF Core 8+ não deve precisar disso.

- [ ] **Step 4: Rodar todos os testes**

```bash
dotnet test
```

Expected: todos os testes passam (unit + integration).

- [ ] **Step 5: Commit**

```bash
git add backend/tests/ImperadorBarberShop.IntegrationTests/ImperadorBarberShop.IntegrationTests.csproj
git add backend/tests/ImperadorBarberShop.IntegrationTests/WebAppFixture.cs
git commit -m "chore(tests): swap Testcontainers for SQLite in-memory"
```
