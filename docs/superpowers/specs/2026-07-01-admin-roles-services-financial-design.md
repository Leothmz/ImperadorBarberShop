# Spec 2 — Área Admin, Serviços com Add-ons e Controle Financeiro

Parte da reforma em 5 etapas: (1) agendamento público ✅, **(2) área admin/barbeiro com papéis — este spec**, (3) integração WhatsApp, (4) bloqueio de agenda por duração, (5) controle de caixa/dashboard.

## Objetivo

Introduzir o papel `Admin` com área de gestão protegida: cadastro de barbeiros, CRUD de serviços (com foto e add-ons), e dashboard financeiro completo. Clientes passam a ver add-ons disponíveis ao agendar. Barbeiros continuam gerenciando apenas a própria agenda.

---

## Modelo de Dados

### Mudanças em entidades existentes

| Entidade | Campo adicionado | Tipo | Obs |
|----------|-----------------|------|-----|
| `UserRole` | `Admin = 2` | enum | Sem backfill — novo valor |
| `Service` | `PhotoUrl` | `string?` | URL Cloudinary |
| `Barber` | `PhotoUrl` | `string?` | URL Cloudinary |

### Nova entidade: `ServiceAddon`

```
ServiceAddon
  ParentServiceId  Guid  FK → Service  (índice)
  AddonServiceId   Guid  FK → Service  (índice)
  PK: (ParentServiceId, AddonServiceId)
  CHECK: ParentServiceId ≠ AddonServiceId
```

Atributos **são serviços** — mesma entidade `Service`, mesmo catálogo. Um serviço pode ser:
- **Standalone** — selecionável diretamente no booking wizard
- **Add-on de outro** — aparece como opção após o cliente selecionar o serviço pai
- **Ambos** — ex: "Barba" é serviço independente e add-on de "Cabelo Masculino" e "Fade"

Add-ons selecionados entram em `AppointmentService` normalmente — sem campo novo no `Appointment`. Preço e duração total = soma de todos os `Service`s no `AppointmentService`.

### Visibilidade de barbeiros inativos

- `GET /barbers` (público): retorna **apenas barbeiros ativos**
- `GET /admin/barbers` (admin): retorna **todos**, com campo `isActive` para distinção

### Sem mudança em

`Appointment`, `AppointmentService`, `Review`, `RefreshToken`, `BarberAvailability`.

### Migration

1. Adiciona coluna `PhotoUrl` (nullable) em `Services`
2. Adiciona coluna `PhotoUrl` (nullable) em `Barbers`
3. Cria tabela `ServiceAddons` com PK composta e CHECK constraint
4. Sem backfill de dados existentes — colunas nullable, tabela vazia

---

## Seed do Admin

**Onde:** `Program.cs`, executado no startup antes de aceitar requests.

**Lógica:**
```
if (Users WHERE Role = Admin).Count() == 0:
    email    = config["Admin:Email"]    // env var ADMIN__EMAIL
    password = config["Admin:Password"] // env var ADMIN__PASSWORD
    if email is null OR password is null:
        throw Exception("Admin credentials not configured. Set ADMIN__EMAIL and ADMIN__PASSWORD.")
    hash = BCrypt.HashPassword(password, cost: 12)
    insert User { Name="Administrador", Email=email, PasswordHash=hash, Role=Admin }
```

**Regras de segurança:**
- Senha **nunca** é logada (nem em Debug/Development)
- Env vars lidas via `IConfiguration` — nunca hardcoded em código ou appsettings commitado
- `appsettings.Development.json` (gitignored) contém placeholder; produção usa variáveis de ambiente da Hostinger VPS
- Seed só roda uma vez — se já existir admin, pula silenciosamente
- Admin não tem entidade `Barber` — é apenas `User(Role=Admin)`

**Troca de senha:** via `PATCH /admin/profile/password` (Admin autenticado). Não existe endpoint de reset público para admin.

---

## API

**Base URL:** `http://localhost:5000/api/v1`

### Auth — mudanças

| Método | Path | Mudança |
|--------|------|---------|
| POST | `/auth/login` | Sem mudança de contrato. Retorna `role=Admin` no JWT quando aplicável. Redirect no frontend. |
| POST | `/auth/register/barber` | **Removido.** Admin cria barbeiros via `/admin/barbers`. |

### Admin — novo grupo `[Authorize(Roles="Admin")]`

#### Gerenciamento de barbeiros

| Método | Path | Descrição |
|--------|------|-----------|
| POST | `/admin/barbers` | Cria barbeiro. Multipart: `name, email, password, availability[]` + `photo` (arquivo). Upload foto → Cloudinary → salva URL. |
| GET | `/admin/barbers` | Lista todos os barbeiros: `id, name, email, photoUrl, averageRating, isActive` |
| PATCH | `/admin/barbers/{id}/deactivate` | Desativa barbeiro (soft). |
| PATCH | `/admin/barbers/{id}/activate` | Reativa barbeiro. |

#### Perfil do admin

| Método | Path | Descrição |
|--------|------|-----------|
| PATCH | `/admin/profile/password` | Troca senha do admin autenticado. Body: `{ currentPassword, newPassword }`. |

#### Gerenciamento de serviços (promovido de público para admin)

| Método | Path | Descrição |
|--------|------|-----------|
| POST | `/services` | Cria serviço. Multipart: `name, description, price, durationMinutes` + `photo`. |
| PUT | `/services/{id}` | Atualiza campos do serviço. Multipart opcional para foto. |
| PATCH | `/services/{id}/deactivate` | Soft-delete (já existe `IsActive`). |
| PATCH | `/services/{id}/activate` | Reativa serviço. |
| POST | `/services/{id}/addons/{addonId}` | Vincula `addonId` como add-on de `id`. 409 se já vinculado. 400 se `id == addonId`. |
| DELETE | `/services/{id}/addons/{addonId}` | Remove vínculo de add-on. |

#### Dashboard financeiro

Todos os endpoints filtram apenas agendamentos com `Status = Completed`.

| Método | Path | Descrição |
|--------|------|-----------|
| GET | `/admin/financial/summary?from=YYYY-MM-DD&to=YYYY-MM-DD` | `{ totalRevenue, totalAppointments, averageTicket, period }` |
| GET | `/admin/financial/by-barber?from=&to=` | Array de `{ barberId, barberName, appointments, revenue }` ordenado por revenue desc |
| GET | `/admin/financial/by-service?from=&to=` | Array de `{ serviceId, serviceName, count, revenue }` ordenado por revenue desc |
| GET | `/admin/financial/export?from=&to=` | CSV com todas as linhas de `AppointmentService` do período: `date, barber, clientName, clientPhone (últimos 4 dígitos mascarados), service, price, appointmentId` |

### Serviços públicos — mudança

`GET /services` passa a incluir add-ons de cada serviço:

```json
[
  {
    "id": "...",
    "name": "Cabelo Masculino",
    "price": 35.00,
    "durationMinutes": 30,
    "photoUrl": "https://res.cloudinary.com/...",
    "addons": [
      { "id": "...", "name": "Barba", "price": 25.00, "durationMinutes": 20, "photoUrl": "..." },
      { "id": "...", "name": "Sobrancelha", "price": 15.00, "durationMinutes": 15, "photoUrl": "..." }
    ]
  }
]
```

---

## Upload de Imagens (Cloudinary)

**Fluxo:**
1. Frontend envia `multipart/form-data` para a API
2. `IImageService.UploadAsync(IFormFile)` faz upload para Cloudinary via SDK oficial
3. Retorna URL pública (`https://res.cloudinary.com/...`)
4. API salva URL no campo `PhotoUrl` da entidade

**Configuração (env vars):**
```
Cloudinary__CloudName=...
Cloudinary__ApiKey=...
Cloudinary__ApiSecret=...
```

**Validação:** máx 5 MB, tipos aceitos `image/jpeg` e `image/png`. Validado no middleware antes de chamar Cloudinary.

---

## Frontend

### Home page (`/`)

- Logo do Imperador (arquivo `public/logo.png`) no header e na seção hero
- `favicon.ico` atualizado com a logo
- Botão **"Área do Barbeiro"** → `/login`

### Login (`/login`)

Sem mudança visual. Após login bem-sucedido:
- `role=Admin` → `/admin/dashboard`
- `role=Barber` → `/barber/dashboard` (comportamento atual)

`/register/barber` removida. Link de auto-cadastro no login removido.

### Middleware (`middleware.ts`)

```
/admin/* → exige cookie role=Admin
/barber/* → exige cookie role=Barber (atual)
```

### Área admin — rotas novas

#### `/admin/dashboard`

- Seletor de período (from / to) com presets: hoje, esta semana, este mês, período customizado
- Cards: receita total, nº atendimentos, ticket médio
- Tabela "Por Barbeiro": foto, nome, atendimentos, receita
- Tabela "Por Serviço": nome, quantidade vendida, receita
- Botão **Exportar CSV** → chama `/admin/financial/export`

#### `/admin/barbers`

- Lista com foto, nome, email, avaliação média, badge ativo/inativo
- Botão **Adicionar Barbeiro** → modal/drawer com form:
  - Nome, email, senha (gerada ou digitada), upload de foto
  - Disponibilidade (mesma UI já usada no `/register/barber`)
- Ações por barbeiro: desativar / reativar

#### `/admin/services`

- Lista com foto, nome, preço, duração, badge ativo/inativo
- Botão **Adicionar Serviço** → form: nome, descrição, preço, duração, foto
- Por serviço: botão **Gerenciar Add-ons** → modal com lista de todos os outros serviços ativos, checkbox para vincular/desvincular
- Ações: editar, desativar/reativar

### Navbar / header admin

Avatar fixo = logo do Imperador (não editável). Nome "Administrador". Botão sair.

### Booking wizard (`/agendar`) — ajuste

**Passo de serviços** (atual: cliente seleciona serviços):
- Após selecionar um serviço que tem add-ons, exibe seção "Deseja adicionar?"
- Cada add-on mostra foto, nome, preço e duração
- Cliente marca/desmarca; preço total e tempo total atualizam em tempo real
- Add-ons selecionados entram no array `serviceIds` do body de `POST /appointments` — sem mudança no contrato da API

---

## Papéis e permissões — resumo

| Ação | Anônimo | Barber | Admin |
|------|---------|--------|-------|
| Agendar | ✅ | — | — |
| Ver slots disponíveis | ✅ | — | — |
| Gerenciar próprio agendamento (token) | ✅ | — | — |
| Ver própria agenda | — | ✅ | — |
| Completar / cancelar agendamento | — | ✅ | — |
| Criar barbeiro | — | — | ✅ |
| CRUD serviços e add-ons | — | — | ✅ |
| Dashboard financeiro | — | — | ✅ |

---

## Fora de escopo (specs futuros)

- Integração WhatsApp (Spec 3)
- Bloqueio de agenda por duração de serviço (Spec 4)
- Relatórios avançados, exportação PDF, metas por barbeiro (Spec 5)
- Reset de senha via email para barbeiros
- Edição de perfil pelo próprio barbeiro (foto, disponibilidade self-service)

---

## Testes

### Backend (unitários)

- `CreateBarberByAdminCommandHandler`: cria User + Barber, faz upload mock de foto, retorna DTO. Falha se email duplicado.
- `CreateServiceCommandHandler`: valida campos, faz upload mock, persiste. Falha se `durationMinutes <= 0` ou `price <= 0`.
- `AddServiceAddonCommandHandler`: vincula add-on. 409 se já existe. 400 se `parentId == addonId`.
- `GetFinancialSummaryQueryHandler`: filtra só `Completed`, agrega por período, calcula ticket médio.
- `AdminSeedService`: seed só quando zero admins; lança exception se env vars ausentes.

### Backend (integração)

- Fluxo completo: login como admin → criar barbeiro → barbeiro faz login → barbeiro vê própria agenda.
- Financial export: criar N appointments `Completed`, exportar CSV, validar linhas.
- Rate limit de seed: não duplica admin em restarts consecutivos.

### Frontend (componente)

- `ServiceCard` com add-ons: exibe seção "Deseja adicionar?" apenas quando `addons.length > 0`.
- Total reactivo no wizard: selecionar add-on atualiza preço e duração na UI.
- Dashboard: renderiza corretamente com dados mock (summary + tabelas + export button).

### Frontend (E2E)

- Admin login → cria barbeiro → barbeiro faz login e vê dashboard.
- Admin cria serviço com add-on → cliente vê add-on no wizard de agendamento.
