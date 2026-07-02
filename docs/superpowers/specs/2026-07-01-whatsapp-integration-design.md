# Spec 3 — Integração WhatsApp

Parte da reforma em 5 etapas: (1) agendamento público ✅, (2) área admin ✅, **(3) integração WhatsApp — este spec**, (4) bloqueio de agenda por duração, (5) controle de caixa/dashboard.

## Objetivo

Adicionar notificações via WhatsApp para clientes e barbeiros em todos os eventos relevantes do ciclo de um agendamento. O canal de comunicação (email, WhatsApp ou ambos) é configurável pelo admin. A conexão do número WhatsApp é gerenciada dentro do próprio dashboard admin via QR code, usando a Evolution API (self-hosted).

---

## Provedor

**Evolution API** — self-hosted, sem custo de API, protocolo WhatsApp Web não-oficial. A instância roda separadamente do backend; o backend se comunica via HTTP usando URL, API key e nome da instância configurados via `AppSettings` no banco.

---

## Modelo de Dados

### Nova tabela `AppSettings`

Chave-valor genérica para configurações de runtime alteráveis pelo admin sem redeploy.

```
AppSettings
  Key    string  PK
  Value  string
```

Chaves usadas pelo sistema:

| Key | Valor padrão | Significado |
|-----|-------------|-------------|
| `notifications:channels` | `"email,whatsapp"` | Canais ativos (CSV: `email`, `whatsapp`) |
| `notifications:reminderMinutesBefore` | `"60"` | Antecedência do lembrete em minutos |
| `whatsapp:evolutionApiUrl` | — | URL base da instância Evolution API |
| `whatsapp:evolutionApiKey` | — | API key da instância |
| `whatsapp:instanceName` | — | Nome da instância WhatsApp |

### Mudança em `Appointments`

| Campo | Tipo | Obs |
|-------|------|-----|
| `ReminderSentAt` | `DateTime?` | Preenchido quando lembrete é enviado. Evita reenvio após restart. |

### Migration

1. Cria tabela `AppSettings` com PK em `Key`
2. Seed das chaves padrão (`notifications:channels = "email,whatsapp"`, `notifications:reminderMinutesBefore = "60"`)
3. Adiciona coluna `ReminderSentAt` (nullable) em `Appointments`

---

## Eventos de Notificação

| # | Evento | Destinatário(s) |
|---|--------|----------------|
| A | Agendamento criado | Barbeiro + Cliente |
| B | Agendamento cancelado (por cliente ou barbeiro) | Cliente |
| C | Lembrete pré-agendamento | Cliente |
| D | Agendamento concluído | Cliente |

O lembrete (C) é disparado quando `ScheduledAt - UtcNow ≤ reminderMinutesBefore` e `ReminderSentAt IS NULL`.

---

## Templates de Mensagem (PT-BR, hardcoded)

| Evento | Destinatário | Mensagem |
|--------|-------------|---------|
| Criado | Barbeiro | `📅 Novo agendamento!\n{clientName} marcou {services} para {data} às {hora}.\nTelefone: {clientPhone}` |
| Criado | Cliente | `✅ Agendamento confirmado!\n{services} com {barberName} em {data} às {hora}.\nGerenciar: {frontendUrl}/agendamento/{token}` |
| Lembrete | Cliente | `⏰ Lembrete: seu agendamento com {barberName} é hoje às {hora}.\nGerenciar: {frontendUrl}/agendamento/{token}` |
| Cancelado | Cliente | `❌ Agendamento cancelado.\n{services} com {barberName} em {data} às {hora} foi cancelado.` |
| Concluído | Cliente | `⭐ Como foi? Deixe sua avaliação:\n{frontendUrl}/agendamento/{token}` |

`{data}` no formato `DD/MM/YYYY`, `{hora}` no formato `HH:mm`. `{services}` = nomes dos serviços separados por vírgula.

---

## Arquitetura Backend

### Camada Application — novos contratos

```csharp
// Infrastructure/Services implementa
interface IWhatsAppService
    Task SendAsync(string phone, string message, CancellationToken ct)
    Task<QrCodeResult> GetQrCodeAsync(CancellationToken ct)
    Task<ConnectionStatus> GetStatusAsync(CancellationToken ct)

// Handlers chamam só este — lê canais da AppSettings e despacha
interface INotificationService
    Task SendAppointmentCreatedAsync(Appointment appointment, Barber barber, CancellationToken ct)
    Task SendAppointmentCancelledAsync(Appointment appointment, Barber barber, CancellationToken ct)
    Task SendAppointmentCompletedAsync(Appointment appointment, Barber barber, CancellationToken ct)
    Task SendReminderAsync(Appointment appointment, Barber barber, CancellationToken ct)

interface IAppSettingsRepository
    Task<string?> GetAsync(string key, CancellationToken ct)
    Task SetAsync(string key, string value, CancellationToken ct)
```

### Camada Infrastructure — implementações

- **`EvolutionApiWhatsAppService`** — `HttpClient` configurado via `IHttpClientFactory`. Endpoints usados:
  - `POST /message/sendText/{instanceName}` — envio de mensagem
  - `GET /instance/connectionState/{instanceName}` — status
  - `GET /instance/connect/{instanceName}` — QR code (base64)
- **`NotificationService`** — lê `notifications:channels` via `IAppSettingsRepository`, monta texto dos templates, chama `IEmailService` e/ou `IWhatsAppService` conforme configuração. Falhas são silenciadas (`best-effort`).
- **`AppSettingsRepository`** — acessa tabela `AppSettings` via EF Core.

### Background Service

```
ReminderBackgroundService : BackgroundService
  loop a cada 60s:
    reminderMinutes = AppSettings["notifications:reminderMinutesBefore"]
    appointments = Appointments
      WHERE Status = Accepted
        AND ReminderSentAt IS NULL
        AND ScheduledAt > UtcNow
        AND ScheduledAt <= UtcNow + reminderMinutes
    foreach appointment:
      NotificationService.SendReminderAsync(appointment, barber)
      appointment.ReminderSentAt = UtcNow
      SaveChanges()
```

### Handlers afetados

| Handler | Mudança |
|---------|---------|
| `CreateAppointmentCommandHandler` | Substitui `IEmailService` por `INotificationService.SendAppointmentCreatedAsync` |
| `CancelAppointmentCommandHandler` | Adiciona `INotificationService.SendAppointmentCancelledAsync` |
| `CancelByBarberCommandHandler` | Adiciona `INotificationService.SendAppointmentCancelledAsync` |
| `CompleteAppointmentCommandHandler` | Adiciona `INotificationService.SendAppointmentCompletedAsync` |

`IEmailService` permanece no projeto — `NotificationService` o chama internamente quando canal `email` está ativo.

---

## API

Todos os endpoints abaixo requerem `[Authorize(Roles="Admin")]`.

### WhatsApp — Conexão

| Método | Path | Descrição |
|--------|------|-----------|
| GET | `/admin/whatsapp/status` | `{ status: "connected"\|"disconnected"\|"qr_required", phoneNumber?: string }` |
| GET | `/admin/whatsapp/qr` | `{ qrCode: "<base64 PNG>" }` — apenas quando `status = qr_required` |
| POST | `/admin/whatsapp/disconnect` | Desconecta instância |

### Notificações — Configurações

| Método | Path | Descrição |
|--------|------|-----------|
| GET | `/admin/notifications/settings` | `{ channels: ["email","whatsapp"], reminderMinutesBefore: 60 }` |
| PUT | `/admin/notifications/settings` | Atualiza canais e/ou janela de lembrete |

---

## Frontend

### Nova rota `/admin/whatsapp`

Link "WhatsApp" adicionado na navbar admin entre "Serviços" e o avatar.

**Aba "Conexão"**
- Badge de status: verde "Conectado" (exibe número), amarelo "Aguardando QR", vermelho "Desconectado"
- Quando `status = qr_required`: exibe imagem QR (`<img src="data:image/png;base64,...">`) com polling via TanStack Query (`refetchInterval: 5000`) até conectar
- Quando conectado: botão "Desconectar" → `POST /admin/whatsapp/disconnect`
- Status revalidado a cada 10s via `refetchInterval`

**Aba "Notificações"**
- Checkboxes: `[ ] Email` `[ ] WhatsApp`
- Input numérico: "Avisar cliente (minutos antes)" — default 60
- Botão "Salvar" → `PUT /admin/notifications/settings`
- Feedback de sucesso/erro inline

---

## Papéis e Permissões — impacto

Sem mudança nas permissões existentes. Notificações são efeito colateral de ações já autorizadas.

---

## Fora de Escopo (specs futuros)

- Respostas do cliente via WhatsApp (bot conversacional)
- Notificação ao barbeiro quando cliente cancela (via token)
- Múltiplas instâncias WhatsApp (um número por barbeiro)
- Personalização de templates pelo admin
- Bloqueio de agenda por duração de serviço (Spec 4)
- Relatórios avançados / metas (Spec 5)

---

## Testes

### Backend (unitários)

- `NotificationService`:
  - Canal `email` ativo → chama `IEmailService`, não chama `IWhatsAppService`
  - Canal `whatsapp` ativo → chama `IWhatsAppService`, não chama `IEmailService`
  - Ambos ativos → chama os dois
  - Nenhum ativo → não chama nenhum
- `ReminderBackgroundService`:
  - Appointment elegível → `SendReminderAsync` chamado, `ReminderSentAt` preenchido
  - Appointment com `ReminderSentAt` preenchido → não reenvia
  - Appointment fora da janela → não envia
- `EvolutionApiWhatsAppService`:
  - `SendAsync` → POST correto para Evolution API
  - `GetStatusAsync` → mapeia resposta para `ConnectionStatus`
  - `GetQrCodeAsync` → retorna base64 do QR

### Backend (integração)

- Criar appointment com `INotificationService` mockado → mock chamado com dados corretos
- Cancelar appointment → mock chamado com evento de cancelamento
- Completar appointment → mock chamado com evento de conclusão

### Frontend (componente)

- Aba Conexão: renderiza QR quando `status = qr_required`, badge verde quando `connected`
- Aba Notificações: submit chama `PUT /admin/notifications/settings`, exibe confirmação de sucesso
