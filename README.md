# O Imperador Barber Shop

> Plataforma completa de agendamento para barbearias — clientes reservam horários online sem cadastro, barbeiros gerenciam sua agenda em tempo real, admins controlam finanças e operações.

---

## Visão Geral

O Imperador Barber Shop é uma aplicação full-stack moderna que conecta clientes e barbeiros através de um sistema de agendamento intuitivo. **Clientes não precisam criar conta** — escolhem o barbeiro, os serviços e o horário, e recebem um link único para acompanhar, cancelar e avaliar o atendimento. Barbeiros gerenciam sua agenda por um painel dedicado. Administradores têm visibilidade total sobre operações, finanças e configurações de notificações.

---

## Funcionalidades

### Para Clientes (sem cadastro)
- Agendamento anônimo em 4 passos: barbeiro → serviços → horário → confirmação
- Link de acesso único por agendamento (token opaco) para gerenciamento
- Cancelamento via link (até 2h antes, apenas agendamentos aceitos)
- Avaliação do serviço via link (1–5 estrelas + comentário, após conclusão)
- Notificação por WhatsApp ou e-mail na criação do agendamento

### Para Barbeiros
- Painel de agendamentos do dia com status em tempo real
- Marcação de serviços como concluídos, com registro de forma de pagamento
- Registro retroativo de pagamento em atendimentos já concluídos
- Cancelamento de emergência de agendamentos aceitos
- Gerenciamento de bloqueios de agenda (pontuais e recorrentes por dia da semana)

> A disponibilidade semanal é editada pelo admin, em `/admin/barbers` — não há editor de horários no painel do barbeiro.

### Para Admins
- Cadastro e gerenciamento de barbeiros
- Gerenciamento do catálogo de serviços (criar, editar, ativar/desativar)
- Painel financeiro com:
  - Resumo de receita, atendimentos, ticket médio, despesas e lucro líquido
  - Comparativo com período anterior
  - Gráfico de receita ao longo do tempo (por dia, semana ou mês)
  - Breakdown por barbeiro e por serviço
  - Exportação CSV
- Gerenciamento de despesas operacionais (texto livre, valor, data)
- Visualização de atendimentos por barbeiro com registro de pagamento
- Gerenciamento de bloqueios de agenda por barbeiro
- Recorrência: lista de clientes quase perdidos (25–30 dias sem visitar) com botão para reconvite via WhatsApp
- Configuração de notificações (canais: e-mail, WhatsApp ou ambos)
- Integração com WhatsApp via Evolution API (QR code, status de conexão)

### Geral
- Proteção contra spam: rate limit por IP (5/hora) e por telefone (3/hora)
- Proteção IDOR: barbeiro só acessa seus próprios recursos
- Rotação de refresh tokens com hash BCrypt
- Anti-double-booking via constraint único `(BarberId, ScheduledAt)` no banco
- Lembretes automáticos de agendamento (background service, configurável)

---

## Stack Tecnológica

### Backend
| Tecnologia | Uso |
|------------|-----|
| ASP.NET Core 9 | API REST |
| Entity Framework Core 9 + SQLite | ORM + banco de dados |
| MediatR | CQRS (Commands & Queries) |
| FluentValidation | Validação de entrada |
| BCrypt.Net | Hash de senhas (custo 12) |
| JWT Bearer | Autenticação (access 15min + refresh 7 dias) |
| MailKit | Envio de e-mails via SMTP |
| Clean Architecture | Domain → Application → Infrastructure → API |

### Frontend
| Tecnologia | Uso |
|------------|-----|
| Next.js 16 (App Router) | Framework React |
| TypeScript | Tipagem estática |
| Tailwind CSS v4 | Estilização com tokens de design |
| TanStack Query v5 | Cache e estado de servidor |
| React Hook Form + Zod | Formulários com validação |
| Axios | HTTP client com interceptor JWT |
| Recharts | Gráficos do dashboard financeiro |

### Infraestrutura
| Tecnologia | Uso |
|------------|-----|
| SQLite (WAL) | Banco de dados principal — arquivo local, sem servidor |
| xUnit + NSubstitute + FluentAssertions | Testes unitários do backend |
| SQLite in-memory | Testes de integração (sem Docker) |
| Vitest + React Testing Library | Testes unitários do frontend |
| MSW v2 | Mock de API nos testes de frontend |
| Playwright | Testes E2E |

---

## Arquitetura

```
ImperadorBarberShop/
├── backend/                        ASP.NET Core 9 — Clean Architecture
│   ├── src/
│   │   ├── Domain/                 Entidades, enums, interfaces de repositório
│   │   ├── Application/            Commands, Queries, Handlers, Validators, DTOs
│   │   ├── Infrastructure/         EF Core, repositórios, JWT, BCrypt, SMTP, WhatsApp
│   │   └── Api/                    Controllers, Middleware, Program.cs
│   └── tests/
│       ├── UnitTests/              Testes unitários (sem I/O)
│       └── IntegrationTests/       Testes HTTP com WebApplicationFactory + SQLite in-memory
│
└── frontend/                       Next.js 16 — App Router
    └── src/
        ├── app/                    Páginas e layouts (rotas)
        ├── components/             Componentes React reutilizáveis
        ├── hooks/                  TanStack Query + lógica de negócio
        ├── lib/api/                Camada HTTP (Axios) tipada
        └── types/                  Tipos TypeScript espelhando DTOs do backend
```

### Decisões de Arquitetura

- **Clean Architecture**: Domain sem dependências externas. Dependências apontam sempre para dentro.
- **CQRS com MediatR**: Cada caso de uso é um Command (escrita) ou Query (leitura). Controllers são dispatchers finos.
- **Handlers co-locados**: Record + Validator + Handler em um único arquivo `.cs`.
- **Segurança de refresh token**: O token bruto viaja num cookie `HttpOnly` emitido pela API; apenas o hash BCrypt é armazenado no banco. Rotacionado a cada uso.
- **IDOR protection**: Toda mutação valida que o `sub`/`barberId` do JWT corresponde ao dono do recurso.
- **Sem mapeamento de claims**: `options.MapInboundClaims = false` preserva nomes originais (`role`, `sub`) nos claims do JWT.
- **Clientes sem conta**: Autenticação por token opaco por agendamento (`AccessToken`) — sem sessão, sem cadastro.

---

## Modelo de Dados

```
User ──< Barber ──< BarberAvailability
  │         │
  │         ├──< BarberBlock
  │         │
  │         └──< Appointment >──< AppointmentService >── Service
  │                   │
  │                   └──< Review
  │
  └──< Expense
  └──< AppSettings
  └──< RefreshToken

Client ──< Appointment   (ClientId opcional; reconhece clientes recorrentes pelo telefone)
```

### Entidades Principais

| Entidade | Campos-chave |
|----------|-------------|
| `User` | Id, Name, Email, PasswordHash, Role (Barber\|Admin) |
| `Barber` | Id, UserId, Availability[], AverageRating |
| `Service` | Id, Name, DurationMinutes, Price, IsActive |
| `Appointment` | Id, ClientName, ClientPhone, ClientId?, AccessToken, BarberId, ScheduledAt, Status, PaymentMethod?, PaidAt?, Notes? |
| `AppointmentService` | AppointmentId, ServiceId (M:N) |
| `Client` | Id, Phone, MatchKey, Name (do 1º agendamento), FirstSeenAt, LastVisitAt?, LastInviteAt?, VisitCount |
| `Review` | Id, AppointmentId, BarberId, Rating (1–5), Comment? |
| `BarberBlock` | Id, BarberId, StartsAt, EndsAt, Description?, IsRecurring, RecurrenceDays? (bitmask), RecurrenceEndsAt? |
| `Expense` | Id, Amount, Description (max 200), Date, CreatedAt, CreatedByUserId |
| `AppSettings` | Id, Key, Value (tabela chave-valor para configurações globais) |
| `RefreshToken` | Id, UserId, TokenHash, ExpiresAt, IsRevoked |

### Catálogo de Serviços

| Serviço | Duração | Preço |
|---------|---------|-------|
| Corte | 30 min | R$ 35,00 |
| Fade / Disfarçado | 40 min | R$ 45,00 |
| Barba | 20 min | R$ 25,00 |
| Sobrancelha | 15 min | R$ 15,00 |
| Hidratação | 20 min | R$ 30,00 |
| Pigmentação | 30 min | R$ 40,00 |

---

## Como Rodar Localmente

### Pré-requisitos

- [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 24+](https://nodejs.org/)

> O banco é SQLite — um arquivo `imperador_barber.db` criado na primeira execução. Sem Docker, sem servidor de banco.

### 1. Clone o repositório

```bash
git clone https://github.com/seu-usuario/ImperadorBarberShop.git
cd ImperadorBarberShop
```

### 2. Configure o backend

As chaves aceitas e os valores de produção estão em [`.env.example`](.env.example). Para
desenvolvimento, crie o arquivo `backend/src/Api/ImperadorBarberShop.Api/appsettings.Development.json`
(gitignored):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=imperador_barber.db"
  },
  "Jwt": {
    "Secret": "<aleatório, 32+ caracteres — openssl rand -base64 48>",
    "Issuer": "ImperadorBarberShop",
    "Audience": "ImperadorBarberShopFrontend",
    "ExpirationMinutes": 15
  },
  "Email": {
    "SmtpHost": "localhost",
    "SmtpPort": 2525,
    "Username": "",
    "Password": "",
    "FromAddress": "noreply@local",
    "FromName": "O Imperador Barber Shop"
  },
  "Admin": {
    "Email": "admin@local",
    "Password": "<senha do admin, criada só no primeiro boot>"
  },
  "Cloudinary": {
    "CloudName": "",
    "ApiKey": "",
    "ApiSecret": ""
  },
  "FrontendUrl": "http://localhost:3000"
}
```

Pontos que derrubam o app se errados:

- **Chaves do JWT**: a seção usa `Secret` e `ExpirationMinutes`. Nomes como `SecretKey` ou
  `AccessTokenExpiryMinutes` são ignorados, e a API responde 500 em **toda** requisição
  (a chave de assinatura fica vazia). O refresh token tem validade fixa de 7 dias.
- **`Admin:Email` / `Admin:Password`**: obrigatórios enquanto o banco não tiver nenhum admin —
  sem eles o processo aborta no boot. O admin é criado uma única vez; trocar a senha aqui
  depois não altera um admin já existente.
- **`Jwt:Secret`** precisa de 32+ caracteres.
- **`Cloudinary`** é opcional: sem ele a API sobe e todo o painel admin funciona, mas o
  envio de fotos responde com um erro explicativo. Preencha para habilitar upload.
- **`Email`** só é necessário se o canal `email` estiver ativo; o envio é best-effort.

> Migrações, `PRAGMA journal_mode=WAL`, seed do catálogo de serviços e o admin inicial rodam
> automaticamente na inicialização, em **todos** os ambientes.

### 3. Inicie o backend

```bash
cd backend
dotnet run --project src/Api/ImperadorBarberShop.Api
# API: http://localhost:5044
# Swagger: http://localhost:5044/swagger
```

### 4. Configure o frontend

```bash
# frontend/.env.local
NEXT_PUBLIC_API_URL=http://localhost:5044
```

`NEXT_PUBLIC_API_URL` é lida em tempo de build (embutida no bundle) e deve ser a origem da
API **sem** o prefixo `/api/v1`.

### 5. Inicie o frontend

```bash
cd frontend
npm install
npm run dev
# App: http://localhost:3000
```

---

## Testes

```bash
# Testes unitários do backend
cd backend && dotnet test tests/ImperadorBarberShop.UnitTests

# Testes de integração (SQLite in-memory, sem Docker)
cd backend && dotnet test tests/ImperadorBarberShop.IntegrationTests

# Todos os testes com cobertura
cd backend && dotnet test --collect:"XPlat Code Coverage"

# Testes unitários do frontend
cd frontend && npm test

# Testes E2E (requer servidor rodando)
cd frontend && npx playwright test
```

---

## API — Endpoints

**Base URL:** `http://localhost:5044/api/v1`  
**Auth:** `Authorization: Bearer <access_token>` (JWT; papéis `Barber` e `Admin` no claim `role`)

### Autenticação (público)

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/auth/login` | Login → `{ accessToken, role, userId, barberId }`; o refresh token vai num cookie `HttpOnly` |
| POST | `/auth/refresh` | Troca o cookie por um novo par (rotaciona o cookie) |
| POST | `/auth/logout` | Limpa o cookie de sessão |

### Serviços

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/services` | — | Listar serviços ativos |
| POST | `/services` | Admin | Criar serviço (`multipart/form-data` com `photo` opcional) |
| PUT | `/services/{id}` | Admin | Editar serviço |
| PATCH | `/services/{id}/activate` | Admin | Ativar serviço |
| PATCH | `/services/{id}/deactivate` | Admin | Desativar serviço |
| DELETE | `/services/{id}` | Admin | Remover serviço |
| POST | `/services/{id}/addons/{addonId}` | Admin | Associar add-on |
| DELETE | `/services/{id}/addons/{addonId}` | Admin | Desassociar add-on |

### Barbeiros

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/barbers` | — | Listar barbeiros (id, nome, avaliação) |
| GET | `/barbers/{id}` | — | Perfil + disponibilidade + avaliação |
| GET | `/barbers/{id}/reviews` | — | Avaliações do barbeiro (sem paginação) |
| GET | `/barbers/{id}/slots?date=&serviceIds=` | — | Slots disponíveis |
| PUT | `/barbers/me/availability` | Barbeiro | Atualizar disponibilidade |
| GET | `/barbers/me/blocks` | Barbeiro | Listar bloqueios |
| POST | `/barbers/me/blocks` | Barbeiro | Criar bloqueio (pontual ou recorrente) |
| DELETE | `/barbers/me/blocks/{id}` | Barbeiro | Remover bloqueio |

### Agendamentos

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/appointments` | — (rate-limited) | Criar agendamento → `{ id, accessToken }` |
| GET | `/appointments/manage/{token}` | — | Status e detalhes pelo token do cliente |
| POST | `/appointments/manage/{token}/cancel` | — | Cancelar pelo token (>2h antes) |
| POST | `/appointments/manage/{token}/review` | — | Avaliar pelo token (após conclusão) |
| GET | `/appointments/barber` | Barbeiro | Agendamentos do barbeiro logado |
| PATCH | `/appointments/{id}/complete` | Barbeiro | Concluir (body opcional: `{ paymentMethod? }`) |
| PATCH | `/appointments/{id}/payment` | Barbeiro | Registrar/atualizar pagamento |
| PATCH | `/appointments/{id}/cancel-by-barber` | Barbeiro | Cancelar por emergência |

### Admin

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/admin/barbers` | Listar barbeiros |
| POST | `/admin/barbers` | Cadastrar barbeiro (`multipart/form-data`, com disponibilidade) |
| PUT | `/admin/barbers/{id}` | Editar barbeiro |
| DELETE | `/admin/barbers/{id}` | Remover barbeiro |
| PATCH | `/admin/barbers/{id}/activate` | Ativar barbeiro |
| PATCH | `/admin/barbers/{id}/deactivate` | Desativar barbeiro |
| PATCH | `/admin/profile/password` | Trocar a própria senha |
| GET | `/admin/barbers/{id}/blocks` | Bloqueios de um barbeiro |
| POST | `/admin/barbers/{id}/blocks` | Criar bloqueio para barbeiro |
| DELETE | `/admin/barbers/{id}/blocks/{blockId}` | Remover bloqueio |
| GET | `/admin/barbers/{id}/appointments` | Atendimentos de um barbeiro |
| GET | `/admin/services` | Listar todos os serviços (inclusive inativos) |
| GET | `/admin/financial/summary?from=&to=` | Resumo financeiro do período |
| GET | `/admin/financial/timeline?from=&to=&groupBy=` | Receita ao longo do tempo |
| GET | `/admin/financial/by-barber?from=&to=` | Receita por barbeiro |
| GET | `/admin/financial/by-service?from=&to=` | Receita por serviço |
| GET | `/admin/financial/export?from=&to=` | Exportar CSV |
| GET | `/admin/financial/expenses?from=&to=` | Listar despesas do período |
| POST | `/admin/financial/expenses` | Registrar despesa |
| DELETE | `/admin/financial/expenses/{id}` | Remover despesa |
| PATCH | `/admin/appointments/{id}/complete` | Concluir atendimento (body opcional: `{ paymentMethod? }`) |
| PATCH | `/admin/appointments/{id}/cancel` | Cancelar atendimento |
| PATCH | `/admin/appointments/{id}/payment` | Registrar pagamento (admin) |
| GET | `/admin/whatsapp/status` | Status da conexão WhatsApp |
| GET | `/admin/whatsapp/qr` | QR code para parear o WhatsApp |
| POST | `/admin/whatsapp/disconnect` | Desconectar |
| GET | `/admin/notifications/settings` | Configurações de notificações |
| PUT | `/admin/notifications/settings` | Atualizar canais de notificação |
| GET | `/admin/clients/reinvite-candidates` | Clientes quase perdidos (25–30 dias sem visitar) elegíveis para reconvite |
| POST | `/admin/clients/{id}/reinvite` | Enviar convite de retorno via WhatsApp e registrar `LastInviteAt` |

---

## Design

| Token | Valor |
|-------|-------|
| Gold | `#C9A84C` |
| Gold Light | `#E8C96A` |
| Gold Dark | `#A8872E` |
| Black | `#0D0D0D` |
| Black Soft | `#1A1A1A` |
| White | `#F5F5F5` |

Fontes: **Montserrat** (títulos) · **Inter** (corpo)

---

## Licença

Repositório privado — sem arquivo `LICENSE` público.
