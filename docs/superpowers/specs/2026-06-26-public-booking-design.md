# Spec 1 — Agendamento Público (sem cadastro de cliente)

Parte de uma reforma maior em 5 etapas: (1) agendamento público — **este spec**, (2) área admin/barbeiro com papéis, (3) integração WhatsApp, (4) bloqueio de agenda por duração de serviço (já parcialmente coberto, validar no contexto novo), (5) controle de caixa / dashboard.

## Objetivo

Cliente agenda sem criar conta: só informa nome, telefone (WhatsApp), barbeiro, serviço(s) e horário. Confirmação é automática (sem etapa de aceite manual do barbeiro). Cliente gerencia (cancela / avalia depois) via link único enviado na confirmação.

## Modelo de dados

### `Appointment`
- Remove `ClientId` (FK para `Users`).
- Adiciona `ClientName` (string, obrigatório).
- Adiciona `ClientPhone` (string, obrigatório, formato BR `+55DDDXXXXXXXXX`).
- Adiciona `AccessToken` (string opaca, ~32 bytes aleatórios em base64url, única, indexada). Gerado na criação. Usado para:
  - página pública de gerenciamento (`/agendamento/{token}`)
  - cancelamento pelo próprio cliente
  - formulário de review pós-`Completed`
- `AppointmentStatus` simplifica de `{Pending, Accepted, Rejected, Cancelled, Completed}` para `{Accepted, Cancelled, Completed}`.
  - Toda criação nasce como `Accepted` (a checagem de vaga livre já acontece na criação).
  - `Cancelled` cobre tanto cancelamento do cliente (via token, regra das 2h) quanto cancelamento do barbeiro (emergência).

### `Review`
- Remove `ClientId` (não existe mais User cliente).
- Mantém `AppointmentId` (FK). Nome/telefone do cliente, quando necessário exibir, vêm via join com `Appointment`.

### `User`
- `Role` enum **não muda neste spec** (Barbeiro continua igual). Cliente deixa de ser `User`.

## Migração de dados

1. Backfill: para `Appointment`s existentes, copiar `Client.Name` → `ClientName`; telefone se existir → `ClientPhone` (senão placeholder `"N/A"`).
2. Backfill de status: `Pending` → `Accepted`, `Rejected` → `Cancelled` (preserva histórico de forma equivalente ao novo modelo).
3. Dropar FK `Appointment.ClientId` → `Users.Id` e a coluna.
4. Dropar coluna `Review.ClientId`.
5. Deletar `User`s com `Role = Client`.
6. Remover `RegisterClientCommand` e o endpoint `POST /auth/register/client`.

## API

| Endpoint | Antes | Depois |
|---|---|---|
| `POST /appointments` | Auth Client, `ClientId` do JWT | **Anônimo**. Body: `BarberId, ScheduledAt, ServiceIds, ClientName, ClientPhone, Notes?`. Valida vaga livre, cria com status `Accepted`. Retorna `accessToken`. |
| `GET /barbers/{id}/slots` | Auth Client | **Anônimo**, lógica de cálculo de slots inalterada |
| `DELETE /appointments/{id}` | Auth Client | **Removido** → `POST /appointments/manage/{token}/cancel` (anônimo, valida regra `ScheduledAt > UtcNow + 2h`) |
| `GET /appointments/manage/{token}` | — (novo) | Anônimo. Retorna dados/status do agendamento pra renderizar a página pública |
| `POST /appointments/manage/{token}/review` | — (novo) | Anônimo. Só permitido se status `Completed`. Cria `Review`. |
| `PATCH /appointments/{id}/accept` | Auth Barber | **Removido** (não há mais etapa de aceite manual) |
| `PATCH /appointments/{id}/reject` | Auth Barber | **Removido** → `PATCH /appointments/{id}/cancel-by-barber` (Auth Barber, só dos próprios agendamentos) |
| `PATCH /appointments/{id}/complete` | Auth Barber | Inalterado |
| `POST /auth/register/client`, login de Client | Existiam | **Removidos** |

### Anti-spam

Rate limiting nativo do ASP.NET Core (`AddRateLimiter`), partition key = `ClientPhone + IP`, fixed-window (ex: 3 criações por hora), aplicado em `POST /appointments`.

## Frontend

- `/client/book` → rota pública `/agendar` (sem `ClientLayout` / middleware de auth).
- Formulário ganha campos **Nome** e **WhatsApp** (com máscara/validação BR) antes da confirmação.
- Tela de confirmação exibe o link `/agendamento/{token}` pra gerenciamento futuro.
- Nova rota pública `/agendamento/[token]`:
  - status `Accepted`: mostra dados + botão cancelar (desabilitado se `ScheduledAt <= UtcNow + 2h`, com mensagem explicando)
  - status `Cancelled`: mostra que foi cancelado
  - status `Completed`: mostra formulário de review (nota 1–5 + comentário opcional)
- Remove páginas de registro/login de cliente e toda a árvore `/client/*` protegida.
- `middleware.ts`: remove checagem de role `Client`; mantém checagem para `/barber/*`.

## Fora de escopo (specs futuros)

- Papéis Admin vs Barbeiro, área de gestão oculta (Spec 2).
- Envio real de WhatsApp (confirmação + lembrete 1h antes) — o link de gerenciamento já existe nesta spec, será incluído nas mensagens quando a integração for construída (Spec 3).
- Dashboard de caixa/métricas (Spec 5).

## Testes

- Backend: testes de unidade para `CreateAppointmentCommandHandler` (sem `ClientId`, validação de `ClientName`/`ClientPhone`, geração de `AccessToken`, status nasce `Accepted`), `CancelAppointmentByTokenCommandHandler` (regra das 2h, token inválido → 404), `CancelByBarberCommandHandler` (autorização só do próprio barbeiro), `CreateReviewByTokenCommandHandler` (só se `Completed`).
- Backend: teste de integração da migração (backfill de status e dados de cliente).
- Backend: teste de rate limiting (excede limite → 429).
- Frontend: testes de componente do form de agendamento (campos obrigatórios, máscara de telefone) e da página `/agendamento/[token]` (renderização por status).
