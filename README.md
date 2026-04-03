# O Imperador Barber Shop

> Plataforma completa de agendamento para barbearias — clientes reservam horários online, barbeiros gerenciam sua agenda em tempo real.

---

## Visão Geral

O Imperador Barber Shop é uma aplicação full-stack moderna que conecta clientes e barbeiros através de um sistema de agendamento intuitivo. Clientes escolhem o barbeiro, os serviços desejados e o horário disponível. Barbeiros recebem notificações, aceitam ou recusam agendamentos e gerenciam toda sua agenda por um painel dedicado.

---

## Funcionalidades

### Para Clientes
- Cadastro e autenticação segura (JWT + refresh token)
- Visualização de barbeiros disponíveis com avaliações e perfis
- Agendamento em 4 passos: barbeiro → serviços → horário → confirmação
- Acompanhamento de agendamentos (próximos e histórico)
- Cancelamento de agendamentos (até 2h antes)
- Avaliação de serviços concluídos (1–5 estrelas + comentário)

### Para Barbeiros
- Painel de gestão de agendamentos em tempo real
- Aceite ou recusa de agendamentos pendentes
- Marcação de serviços como concluídos
- Configuração de disponibilidade semanal
- Perfil público com avaliações dos clientes

### Geral
- Notificações por e-mail automáticas (novo agendamento, aceito, recusado)
- Design responsivo com identidade visual premium (dourado e preto)
- Autenticação com rotação de refresh tokens e proteção IDOR

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

### Infraestrutura
| Tecnologia | Uso |
|------------|-----|
| PostgreSQL 16 | Banco de dados principal |
| Docker / Docker Compose | Ambiente local de desenvolvimento |
| xUnit + NSubstitute | Testes unitários do backend |
| Testcontainers | Testes de integração com PostgreSQL real |
| Vitest + React Testing Library | Testes unitários do frontend |
| Playwright | Testes E2E |

---

## Arquitetura

```
ImperadorBarberShop/
├── backend/                        ASP.NET Core 9 — Clean Architecture
│   ├── src/
│   │   ├── Domain/                 Entidades, enums, interfaces de repositório
│   │   ├── Application/            Commands, Queries, Handlers, Validators, DTOs
│   │   ├── Infrastructure/         EF Core, repositórios, JWT, BCrypt, SMTP
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
- **Segurança de refresh token**: Token bruto retornado ao cliente; apenas o hash BCrypt é armazenado no banco. Token é rotacionado a cada uso.
- **IDOR protection**: Toda mutação valida que o `sub`/`barberId` do JWT corresponde ao dono do recurso.
- **Sem mapeamento de claims**: `options.MapInboundClaims = false` preserva nomes originais (`role`, `sub`) nos claims do JWT.

---

## Modelo de Dados

```
User ──< Barber ──< BarberAvailability
  │         │
  │         └──< Appointment >──< AppointmentService >── Service
  │                   │
  └─────────────< Review
```

### Entidades Principais

| Entidade | Campos-chave |
|----------|-------------|
| `User` | Id, Name, Email, PasswordHash, Role (Client\|Barber) |
| `Barber` | Id, UserId, Availability[], AverageRating |
| `Service` | Id, Name, DurationMinutes, Price, IsActive |
| `Appointment` | Id, ClientId, BarberId, ScheduledAt, Status, Notes |
| `Review` | Id, AppointmentId, Rating (1–5), Comment |

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
- [Node.js 20+](https://nodejs.org/)

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
    "Secret": "<string-aleatória-mínimo-32-caracteres>",
    "Issuer": "ImperadorBarberShop",
    "Audience": "ImperadorBarberShopFrontend",
    "ExpirationMinutes": 15
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
# API: http://localhost:5044
# Swagger: http://localhost:5044/swagger
```

### 5. Configure o frontend

```bash
# frontend/.env.local
NEXT_PUBLIC_API_URL=http://localhost:5044/api/v1
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

## API — Endpoints Principais

**Base URL:** `http://localhost:5044/api/v1`  
**Auth:** `Authorization: Bearer <token>`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/auth/register/client` | — | Cadastro de cliente |
| POST | `/auth/register/barber` | — | Cadastro de barbeiro |
| POST | `/auth/login` | — | Login |
| POST | `/auth/refresh` | — | Renovar token |
| GET | `/services` | — | Listar serviços |
| GET | `/barbers` | — | Listar barbeiros |
| GET | `/barbers/{id}` | — | Perfil do barbeiro |
| GET | `/barbers/{id}/slots` | Cliente | Slots disponíveis |
| POST | `/appointments` | Cliente | Criar agendamento |
| GET | `/appointments/mine` | Cliente | Meus agendamentos |
| DELETE | `/appointments/{id}` | Cliente | Cancelar agendamento |
| GET | `/appointments/barber` | Barbeiro | Agendamentos do barbeiro |
| PATCH | `/appointments/{id}/accept` | Barbeiro | Aceitar agendamento |
| PATCH | `/appointments/{id}/reject` | Barbeiro | Recusar agendamento |
| PATCH | `/appointments/{id}/complete` | Barbeiro | Concluir agendamento |
| POST | `/reviews` | Cliente | Avaliar serviço |

---

## Design

| Token | Valor |
|-------|-------|
| Gold | `#C9A84C` |
| Gold Light | `#E8C96A` |
| Black | `#0D0D0D` |
| Black Soft | `#1A1A1A` |
| White | `#F5F5F5` |

Fontes: **Montserrat** (títulos) · **Inter** (corpo)

---

## Licença

MIT — veja [LICENSE](LICENSE) para detalhes.
