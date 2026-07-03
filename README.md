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
- Configuração de disponibilidade semanal (janelas por dia da semana)
- Gerenciamento de bloqueios de agenda (pontuais e recorrentes por dia da semana)

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
| Entity Framework Core 9 + Npgsql | ORM + PostgreSQL |
| MediatR | CQRS (Commands & Queries) |
| FluentValidation | Validação de entrada |
| BCrypt.Net | Hash de senhas (custo 12) |
| JWT Bearer | Autenticação (access 15min + refresh 7 dias) |
| MailKit | Envio de e-mails via SMTP |
| Clean Architecture | Domain → Application → Infrastructure → API |

### Frontend
| Tecnologia | Uso |
|------------|-----|
| Next.js 15 (App Router) | Framework React |
| TypeScript | Tipagem estática |
| Tailwind CSS v4 | Estilização com tokens de design |
| TanStack Query v5 | Cache e estado de servidor |
| React Hook Form + Zod | Formulários com validação |
| Axios | HTTP client com interceptor JWT |
| Recharts | Gráficos do dashboard financeiro |

### Infraestrutura
| Tecnologia | Uso |
|------------|-----|
| PostgreSQL 16 | Banco de dados principal |
| Docker / Docker Compose | Ambiente local de desenvolvimento |
| xUnit + NSubstitute + FluentAssertions | Testes unitários do backend |
| Testcontainers | Testes de integração com PostgreSQL real |
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
│       └── IntegrationTests/       Testes com banco real via Testcontainers
│
└── frontend/                       Next.js 15 — App Router
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
- **Segurança de refresh token**: Token bruto retornado ao cliente; apenas o hash BCrypt é armazenado no banco. Rotacionado a cada uso.
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
```

### Entidades Principais

| Entidade | Campos-chave |
|----------|-------------|
| `User` | Id, Name, Email, PasswordHash, Role (Barber\|Admin) |
| `Barber` | Id, UserId, Availability[], AverageRating |
| `Service` | Id, Name, DurationMinutes, Price, IsActive |
| `Appointment` | Id, ClientName, ClientPhone, AccessToken, BarberId, ScheduledAt, Status, PaymentMethod?, PaidAt?, Notes? |
| `AppointmentService` | AppointmentId, ServiceId (M:N) |
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

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 24+](https://nodejs.org/)

### 1. Clone o repositório

```bash
git clone https://github.com/seu-usuario/ImperadorBarberShop.git
cd ImperadorBarberShop
```

### 2. Suba o banco de dados

```bash
docker-compose up -d
```

### 3. Configure o backend

Crie o arquivo `backend/src/Api/ImperadorBarberShop.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=imperador_barber;Username=imperador;Password=localdev"
  },
  "Jwt": {
    "SecretKey": "<string-aleatória-mínimo-32-caracteres>",
    "Issuer": "ImperadorBarberShop",
    "Audience": "ImperadorBarberShopFrontend",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "Email": {
    "SmtpHost": "smtp.mailtrap.io",
    "SmtpPort": 587,
    "Username": "<mailtrap-user>",
    "Password": "<mailtrap-pass>",
    "FromAddress": "noreply@imperadorbarber.com",
    "FromName": "O Imperador Barber Shop"
  },
  "FrontendUrl": "http://localhost:3000"
}
```

> As migrações são aplicadas automaticamente na inicialização em ambiente Development.

### 4. Inicie o backend

```bash
cd backend
dotnet run --project src/Api/ImperadorBarberShop.Api
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

### 5. Configure o frontend

```bash
# frontend/.env.local
NEXT_PUBLIC_API_URL=http://localhost:5000
```

### 6. Inicie o frontend

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

# Testes de integração (requer Docker)
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

**Base URL:** `http://localhost:5000/api/v1`  
**Auth:** `Authorization: Bearer <token>`

### Autenticação (público)

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/auth/register/barber` | Cadastro de barbeiro (inclui disponibilidade) |
| POST | `/auth/login` | Login → `{ accessToken, refreshToken, role, userId, barberId }` |
| POST | `/auth/refresh` | Renovar token |

### Serviços (público)

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/services` | Listar serviços ativos |

### Barbeiros

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/barbers` | — | Listar barbeiros (id, nome, avaliação) |
| GET | `/barbers/{id}` | — | Perfil + disponibilidade + avaliação |
| GET | `/barbers/{id}/reviews` | — | Avaliações paginadas |
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
| POST | `/admin/barbers` | Cadastrar barbeiro |
| GET | `/admin/barbers/{id}/blocks` | Bloqueios de um barbeiro |
| POST | `/admin/barbers/{id}/blocks` | Criar bloqueio para barbeiro |
| DELETE | `/admin/barbers/{id}/blocks/{blockId}` | Remover bloqueio |
| GET | `/admin/barbers/{id}/appointments` | Atendimentos de um barbeiro |
| GET | `/admin/services` | Listar todos os serviços |
| POST | `/admin/services` | Criar serviço |
| PATCH | `/admin/services/{id}` | Editar serviço |
| GET | `/admin/financial/summary?from=&to=` | Resumo financeiro do período |
| GET | `/admin/financial/timeline?from=&to=&groupBy=` | Receita ao longo do tempo |
| GET | `/admin/financial/by-barber?from=&to=` | Receita por barbeiro |
| GET | `/admin/financial/by-service?from=&to=` | Receita por serviço |
| GET | `/admin/financial/export?from=&to=` | Exportar CSV |
| GET | `/admin/financial/expenses?from=&to=` | Listar despesas do período |
| POST | `/admin/financial/expenses` | Registrar despesa |
| DELETE | `/admin/financial/expenses/{id}` | Remover despesa |
| PATCH | `/admin/appointments/{id}/payment` | Registrar pagamento (admin) |
| GET | `/admin/whatsapp/status` | Status da conexão WhatsApp |
| POST | `/admin/whatsapp/connect` | Iniciar conexão (gera QR) |
| POST | `/admin/whatsapp/disconnect` | Desconectar |
| GET | `/admin/settings/notifications` | Configurações de notificações |
| PUT | `/admin/settings/notifications` | Atualizar canais de notificação |

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

MIT — veja [LICENSE](LICENSE) para detalhes.
