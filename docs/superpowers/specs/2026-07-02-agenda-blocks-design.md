# Spec 4 — Bloqueio Manual de Agenda

Parte da reforma em 5 etapas: (1) agendamento público ✅, (2) área admin ✅, (3) WhatsApp ✅, **(4) bloqueio de agenda — este spec**, (5) controle de caixa.

## Objetivo

Permitir que barbeiros (e admins) bloqueiem manualmente períodos da agenda — almoço, folga, licença — impedindo que clientes agendem nesses horários. Bloqueios podem ser pontuais ou recorrentes (dias da semana selecionáveis, com ou sem data de fim).

---

## Modelo de Dados

### Nova entidade: `BarberBlock`

```
BarberBlock
  Id               Guid        PK
  BarberId         Guid        FK → Barber (índice)
  StartsAt         DateTime    UTC — início do bloco (hora do dia)
  EndsAt           DateTime    UTC — fim do bloco (hora do dia)
  Description      string?     max 200 chars
  IsRecurring      bool        false = pontual, true = recorrente
  RecurrenceDays   int?        bitmask: Dom=1, Seg=2, Ter=4, Qua=8, Qui=16, Sex=32, Sáb=64
  RecurrenceEndsAt DateTime?   null = sem fim
  CreatedAt        DateTime    UTC
```

**Regras:**
- `StartsAt < EndsAt`
- Se `IsRecurring = false`: `RecurrenceDays` e `RecurrenceEndsAt` são null
- Se `IsRecurring = true`: `RecurrenceDays` tem ao menos 1 bit setado; `RecurrenceEndsAt` pode ser null (sem fim)
- Bloco recorrente é ativo em um dado dia se: o dia da semana tem bit setado em `RecurrenceDays` E (`RecurrenceEndsAt` é null OU a data ≤ `RecurrenceEndsAt`)

**Bitmask de dias:**

| Dom | Seg | Ter | Qua | Qui | Sex | Sáb |
|-----|-----|-----|-----|-----|-----|-----|
| 1   | 2   | 4   | 8   | 16  | 32  | 64  |

Exemplo: Seg + Qua + Sex = `2 + 8 + 32 = 42`

---

## API

### Barbeiro (própria agenda)

Todos os endpoints abaixo requerem `RequireBarberRole`.

| Método | Path | Descrição |
|--------|------|-----------|
| `GET` | `/api/v1/barbers/me/blocks` | Lista blocos do barbeiro logado (ativos e futuros) |
| `POST` | `/api/v1/barbers/me/blocks` | Cria bloco pontual ou recorrente |
| `DELETE` | `/api/v1/barbers/me/blocks/{id}` | Remove bloco (IDOR: só do próprio barbeiro) |

**POST body:**
```json
{
  "startsAt": "2026-07-10T12:00:00Z",
  "endsAt": "2026-07-10T13:00:00Z",
  "description": "Almoço",
  "isRecurring": true,
  "recurrenceDays": 42,
  "recurrenceEndsAt": null
}
```

**GET response** (lista):
```json
[
  {
    "id": "...",
    "startsAt": "2026-07-10T12:00:00Z",
    "endsAt": "2026-07-10T13:00:00Z",
    "description": "Almoço",
    "isRecurring": true,
    "recurrenceDays": 42,
    "recurrenceEndsAt": null,
    "createdAt": "..."
  }
]
```

Sem endpoint de edição — delete + recria.

### Admin (qualquer barbeiro)

Todos os endpoints abaixo requerem `RequireAdminRole`.

| Método | Path | Descrição |
|--------|------|-----------|
| `GET` | `/api/v1/admin/barbers/{barberId}/blocks` | Lista blocos de qualquer barbeiro |
| `POST` | `/api/v1/admin/barbers/{barberId}/blocks` | Cria bloco para qualquer barbeiro |
| `DELETE` | `/api/v1/admin/barbers/{barberId}/blocks/{id}` | Remove bloco de qualquer barbeiro |

Mesmos payloads e responses dos endpoints de barbeiro.

---

## Impacto em Funcionalidades Existentes

### Endpoint de slots disponíveis

`GET /api/v1/barbers/{id}/slots?date=YYYY-MM-DD&serviceIds=...`

A lógica atual filtra `Appointments` com status `Accepted`. Precisa também excluir slots que colidem com `BarberBlock` ativos na data solicitada:

1. Buscar todos os blocos do barbeiro onde:
   - Pontual: `StartsAt.Date == date`
   - Recorrente: bit do dia da semana setado em `RecurrenceDays` E (`RecurrenceEndsAt` is null OR `date <= RecurrenceEndsAt`)
2. Para cada slot candidato, rejeitar se `[slot.Start, slot.End)` sobrepõe qualquer bloco

---

## Validação

**POST `/barbers/me/blocks` e `/admin/barbers/{id}/blocks`:**
- `startsAt` obrigatório
- `endsAt` obrigatório, deve ser > `startsAt`
- `description` opcional, max 200 chars
- Se `isRecurring = true`: `recurrenceDays` obrigatório e > 0; valor máximo = 127 (todos os dias)
- Se `isRecurring = false`: `recurrenceDays` deve ser null/ausente

---

## Frontend

### Área do barbeiro (`/barber/dashboard`)

Nova aba **"Bloqueios"** na dashboard:
- Lista de blocos ativos/futuros: data/hora início-fim, descrição, badge "Recorrente" com dias, botão excluir
- Botão "Adicionar bloqueio" → modal com:
  - Campos: data início, hora início, data fim, hora fim, descrição (opcional)
  - Toggle "Recorrente":
    - Se ativo: checkboxes dos 7 dias da semana + datepicker "Repetir até" (opcional)
    - Se inativo: campos de data/hora são para um evento único
  - Botão salvar

### Área admin (`/admin/barbers`)

Na página existente de barbeiros, ao expandir/selecionar um barbeiro: nova seção **"Bloqueios"** com a mesma UI da dashboard do barbeiro (lista + modal de criação + exclusão).

### Agendamento público (`/agendar`)

Nenhuma mudança visual — slots bloqueados simplesmente não aparecem.

---

## Testes

**Backend:**
- Unit: handler `CreateBarberBlock` — validações, IDOR
- Unit: lógica de sobreposição de slot com bloco pontual e recorrente
- Integration: CRUD completo via API (barbeiro e admin)
- Integration: `GET /slots` não retorna horários bloqueados

**Frontend:**
- Componente de lista de blocos renderiza corretamente
- Modal de criação — campos recorrentes aparecem só quando toggle ativo
- Exclusão chama endpoint correto

---

## Migrações EF

Uma migration: `AddBarberBlocks`
- Tabela `BarberBlocks` com índice em `BarberId`
