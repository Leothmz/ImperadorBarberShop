# Spec — Migração para SQLite

**Contexto:** Parte da eliminação de dependência de infraestrutura. Remove PostgreSQL + Docker do ambiente de desenvolvimento e de produção. O banco passa a ser um arquivo `.db` gerenciado pelo SQLite, sem serviço externo.

---

## Objetivo

Trocar o provider de banco de dados de `Npgsql` (PostgreSQL) para `Microsoft.EntityFrameworkCore.Sqlite`, eliminando:
- A necessidade de Docker para rodar a aplicação localmente
- A necessidade de gerenciar um serviço PostgreSQL em produção (Hostinger VPS)

---

## Modelo de Dados

Nenhuma entidade muda. O schema permanece idêntico. O que muda é como EF Core persiste cada tipo:

| Tipo C# | Postgres | SQLite (EF Core 8) |
|---------|----------|--------------------|
| `Guid` | `uuid` | `TEXT` (formato `xxxxxxxx-xxxx-...`) |
| `DateTime` (UTC) | `timestamptz` | `TEXT` ISO 8601 — EF Core SQLite trata automaticamente |
| `DateOnly` | `date` | `TEXT` `YYYY-MM-DD` — converter built-in no EF Core 8 |
| `TimeOnly` | `time` | `TEXT` `HH:mm:ss.fffffff` — converter built-in no EF Core 8 |
| `decimal` | `numeric(10,2)` | `TEXT` (para preservar precisão) — comportamento padrão do EF Core SQLite |
| `string` | `character varying(N)` | `TEXT` — `MaxLength` ainda gera constraint de validação |
| `int` / `enum` | `integer` | `INTEGER` |
| `bool` | `boolean` | `INTEGER` (0/1) |

---

## Mudanças no Código

### 1. Pacotes NuGet

**`ImperadorBarberShop.Infrastructure.csproj`:**
```xml
<!-- Remover -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />

<!-- Adicionar -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
```

**`ImperadorBarberShop.IntegrationTests.csproj`:**
```xml
<!-- Remover -->
<PackageReference Include="Testcontainers.PostgreSql" Version="4.4.0" />

<!-- Adicionar (se não existir) -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.6" />
```

### 2. DependencyInjection.cs

```csharp
// Antes
options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))

// Depois
options.UseSqlite(configuration.GetConnectionString("DefaultConnection"))
```

### 3. AppDbContextFactory.cs (usado por `dotnet ef migrations`)

```csharp
// Antes
optionsBuilder.UseNpgsql(connectionString);

// Depois
optionsBuilder.UseSqlite(connectionString);
```

### 4. Configurações de entidade — remover tipos Postgres

Três arquivos têm `HasColumnType()` com nomes de tipo Postgres-específicos. Remover essas chamadas (o SQLite provider define seus próprios tipos):

- `BarberConfiguration.cs`: remover `.HasColumnType("decimal(3,2)")`
- `ExpenseConfiguration.cs`: remover `.HasColumnType("numeric(10,2)")`
- `ServiceConfiguration.cs`: remover `.HasColumnType("decimal(10,2)")`

### 5. WAL mode

Habilitar WAL (Write-Ahead Logging) na inicialização para suportar leituras concorrentes. Em `DependencyInjection.cs` ou `Program.cs`, após registrar o DbContext:

```csharp
// Habilitar WAL mode — permite leituras concorrentes durante escritas
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
```

Ou via connection string: `Data Source=imperador_barber.db;Mode=ReadWriteCreate;Cache=Shared`

### 6. Migrations

Deletar todas as migrations existentes (são Postgres-específicas) e criar uma nova:

```bash
# Na pasta backend/
rm -rf src/Infrastructure/ImperadorBarberShop.Infrastructure/Migrations/

dotnet ef migrations add InitialCreate \
  --project src/Infrastructure/ImperadorBarberShop.Infrastructure \
  --startup-project src/Api/ImperadorBarberShop.Api
```

### 7. appsettings.Development.json

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=imperador_barber.db"
}
```

O arquivo `imperador_barber.db` será criado no diretório de execução do projeto. Adicionar ao `.gitignore`:
```
*.db
*.db-shm
*.db-wal
```

### 8. docker-compose.yml

Deletar — não é mais necessário.

---

## Testes de Integração

`WebAppFixture.cs` usa `Testcontainers.PostgreSql` para subir um container Postgres por test run. Trocar por **SQLite em memória**, que é mais rápido e sem dependência de Docker:

```csharp
// Antes
private PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
// ...
options.UseNpgsql(_postgres.GetConnectionString())

// Depois
options.UseSqlite($"Data Source=:memory:")
// ou, para isolar por test class:
options.UseSqlite($"Data Source=test_{Guid.NewGuid():N}.db")
```

SQLite `:memory:` cria um banco em RAM, descartado ao fechar a conexão. Migrations são aplicadas no `InitializeAsync()` igual ao atual.

**Atenção:** SQLite em memória com EF Core precisa manter a conexão aberta durante o test run. A fixture deve guardar e reusar a `SqliteConnection`:

```csharp
private SqliteConnection _connection = new("Data Source=:memory:");

// No InitializeAsync:
_connection.Open();
options.UseSqlite(_connection); // passa a conexão aberta, não a string
```

---

## Deploy na Hostinger VPS

O arquivo `.db` fica no servidor ao lado da aplicação. Configurar via variável de ambiente ou `appsettings.Production.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=/var/data/imperador_barber.db"
}
```

Criar o diretório antes do primeiro deploy:
```bash
mkdir -p /var/data && chown www-data:www-data /var/data
```

**Backup:** copiar o arquivo `.db` (com o servidor parado ou com WAL mode ativo, que permite leitura consistente).

---

## O que NÃO muda

- Todas as entidades de domínio
- Repositórios e queries LINQ
- Handlers, Commands, DTOs
- Controllers, JWT, BCrypt, MailKit
- Frontend

---

## Riscos e Limitações

| Risco | Mitigação |
|-------|-----------|
| Writes bloqueiam outros writes (SQLite lock) | WAL mode resolve para carga de barbearia (baixa concorrência) |
| Sem suporte a múltiplos servidores (SQLite é local) | Não aplicável — app roda em um único VPS |
| Sem tipo `timestamptz` nativo | EF Core armazena como TEXT ISO 8601; `DateTime.Kind = Utc` preservado em código |
| Decimal stored as TEXT | Pode ter impacto mínimo de performance em queries de soma; irrelevante na escala do app |
