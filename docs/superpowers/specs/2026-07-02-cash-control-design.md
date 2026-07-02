# Spec 5 — Controle de Caixa

Parte da reforma em 5 etapas: (1) agendamento público ✅, (2) área admin ✅, (3) WhatsApp ✅, (4) bloqueio de agenda ✅, **(5) controle de caixa — este spec**.

## Objetivo

Adicionar rastreamento de forma de pagamento por agendamento, registro de despesas do salão e melhorias visuais no dashboard financeiro (gráfico de receita×tempo e comparativo de período).

---

## Modelo de Dados

### Mudanças em `Appointment`

Dois campos novos, ambos opcionais:

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `PaymentMethod` | `PaymentMethod?` (enum nullable) | Forma de pagamento |
| `PaidAt` | `DateTime?` UTC | Preenchido automaticamente quando `PaymentMethod` é definido |

```csharp
public enum PaymentMethod
{
    Dinheiro = 0,
    Cartão   = 1,
    Pix      = 2,
}
```

**Regra:** `PaymentMethod` é puramente informativo. `AppointmentStatus.Completed` continua sendo o critério de receita. Se `PaymentMethod` for informado, `PaidAt` é definido como `DateTime.UtcNow`. Se o método não for informado ao concluir, o agendamento entra na receita normalmente (sem `PaidAt`).

### Nova entidade: `Expense`

```
Expense
  Id               Guid        PK
  Amount           decimal     positivo, > 0
  Description      string      max 200 chars — texto livre, serve como categoria
  Date             DateOnly    data da despesa (pode ser retroativa)
  CreatedAt        DateTime    UTC
  CreatedByUserId  Guid        FK → User (índice)
```

**Regras:**
- Apenas Admin pode criar e excluir despesas
- `Amount` deve ser > 0
- `Date` pode ser qualquer data (retroativo permitido)
- Sem edição — delete + recria

### Migration

1. Adiciona `PaymentMethod` (nullable int) e `PaidAt` (nullable timestamptz) em `Appointments`
2. Cria tabela `Expenses` com FK para `Users` e índice em `CreatedByUserId`

---

## API

**Base URL:** `http://localhost:5000/api/v1`

### Mudanças em endpoints existentes

| Método | Path | Auth | Mudança |
|--------|------|------|---------|
| `PATCH` | `/appointments/{id}/complete` | Barber | Body aceita `{ paymentMethod?: "Dinheiro"\|"Cartão"\|"Pix" }` opcional. Se informado, salva método e `PaidAt = UtcNow`. Se omitido, conclui normalmente sem método. |
| `GET` | `/admin/financial/summary?from=&to=` | Admin | Response passa a incluir `totalExpenses` (decimal) e `netRevenue` (= totalRevenue − totalExpenses) |

### Novos endpoints

#### Pagamento

| Método | Path | Auth | Descrição |
|--------|------|------|-----------|
| `PATCH` | `/appointments/{id}/payment` | Barber (próprio) / Admin | Define ou atualiza forma de pagamento de um agendamento `Completed`. Body: `{ paymentMethod: "Dinheiro"\|"Cartão"\|"Pix" }`. Salva `PaidAt = UtcNow` se ainda não definido. IDOR: barbeiro só acessa próprios agendamentos. |

#### Despesas

| Método | Path | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/admin/financial/expenses?from=YYYY-MM-DD&to=YYYY-MM-DD` | Admin | Lista despesas do período (filtro por `Date`). Retorna `[{ id, amount, description, date, createdAt }]` ordenado por `date` desc. |
| `POST` | `/admin/financial/expenses` | Admin | Cria despesa. Body: `{ amount, description, date }`. Retorna `{ id }`. |
| `DELETE` | `/admin/financial/expenses/{id}` | Admin | Remove despesa. 404 se não encontrada. |

#### Timeline financeira

| Método | Path | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/admin/financial/timeline?from=YYYY-MM-DD&to=YYYY-MM-DD&groupBy=day\|week\|month` | Admin | Receita de agendamentos `Completed` agrupada por período. Retorna `[{ period: "YYYY-MM-DD", revenue: decimal, appointments: int }]`. `period` é sempre a data de início do grupo (primeiro dia da semana/mês). |

### Respostas de erro

- `400` — validação (amount ≤ 0, datas inválidas, paymentMethod inválido)
- `404` — recurso não encontrado
- `403` — tentativa de acessar agendamento de outro barbeiro

---

## Validação

**`PATCH /appointments/{id}/payment`:**
- Agendamento deve ter `Status == Completed`
- `paymentMethod` obrigatório, deve ser valor válido do enum
- Barbeiro: `BarberId` do JWT deve corresponder ao agendamento

**`POST /admin/financial/expenses`:**
- `amount` obrigatório, > 0
- `description` obrigatório, max 200 chars
- `date` obrigatório, formato `YYYY-MM-DD`

**`GET /admin/financial/timeline`:**
- `groupBy` aceita apenas `"day"`, `"week"`, `"month"` (default: `"day"`)
- `from` e `to` obrigatórios

---

## Frontend

### Barber dashboard (`/barber/dashboard`)

**Modal de conclusão de agendamento** — ao marcar como concluído, exibir select opcional de pagamento antes de confirmar:

```
Forma de pagamento (opcional)
○ Dinheiro  ○ Cartão  ○ Pix  ○ Pular
```

Se "Pular" ou nenhuma opção selecionada, conclui sem método. Não bloqueia a conclusão.

### Admin — agendamentos (`/admin/dashboard` ou área de agendamentos)

Cada agendamento `Completed` na lista do barbeiro mostra:
- Badge do método: `💵 Dinheiro` / `💳 Cartão` / `⚡ Pix` / `— sem método`
- Se sem método: botão "Registrar pagamento" → select inline + confirmar

### Admin — dashboard financeiro (`/admin/dashboard`)

Página existente expandida com quatro blocos:

#### 1. Cards de resumo (existente, atualizado)

Cards atuais: `Receita total`, `Agendamentos`, `Ticket médio`.
Novos cards adicionados: `Despesas` e `Lucro líquido` (= Receita − Despesas).

Cada card mostra variação percentual vs período anterior de mesmo tamanho:
- `↑ 12%` (verde) ou `↓ 8%` (vermelho) vs período anterior

Exemplo: período selecionado = 1–31 julho → comparativo = 1–30 junho.

#### 2. Gráfico de receita×tempo (novo)

- Lib: **Recharts** (`BarChart` para períodos curtos, `LineChart` para longos — ou sempre `BarChart`)
- Seletor de agrupamento: `Dia` / `Semana` / `Mês`
- Eixo X: período, Eixo Y: R$
- Cor: `brand-gold` (#C9A84C)
- Tooltip com valor formatado em R$

#### 3. Seção Despesas (nova)

- Formulário inline: campo `Valor` (number) + `Descrição` (text) + `Data` (date) + botão Adicionar
- Lista de despesas do período: descrição, data, valor, botão excluir (com `window.confirm`)
- Total de despesas mostrado no rodapé da lista

#### 4. Comparativo de período

Mostrado inline nos cards de resumo (setas + percentual, conforme item 1 acima).

**Implementação:** comparativo é calculado no frontend com duas chamadas a `GET /admin/financial/summary` — uma para o período selecionado, outra para o período anterior de mesmo tamanho (calculado como `from - duração` até `from - 1 dia`). Nenhum endpoint novo.

---

## Testes

### Backend

**Unit:**
- `UpdatePaymentMethodCommand` — happy path, agendamento não-Completed retorna erro, IDOR
- `CreateExpenseCommand` — amount ≤ 0 retorna erro, campos obrigatórios
- `GetFinancialTimelineQuery` — agrupamento por dia/semana/mês correto
- `GetFinancialSummaryQuery` — totalExpenses e netRevenue corretos

**Integration:**
- `PATCH /appointments/{id}/payment` — 204 happy, 400 invalid method, 403 wrong barber, 404 not found
- `POST /admin/financial/expenses` — 201 happy, 400 validation
- `DELETE /admin/financial/expenses/{id}` — 204 happy, 404 not found
- `GET /admin/financial/expenses` — filtra por período corretamente
- `GET /admin/financial/timeline` — retorna agrupamento correto
- `GET /admin/financial/summary` — inclui totalExpenses e netRevenue

### Frontend

**Unit:**
- Modal de conclusão mostra seletor de pagamento, "Pular" não bloqueia conclusão
- Dashboard renderiza novos cards (Despesas, Lucro líquido)
- Gráfico de timeline renderiza com dados mockados
- Seção de despesas: adicionar + listar + excluir

---

## Dependências

### Nova dependência frontend

```bash
npm install recharts
npm install --save-dev @types/recharts  # se necessário
```

Recharts ~380KB bundled, ~47KB gzipped. Nenhuma outra dependência nova.

### Variáveis de ambiente

Nenhuma nova variável necessária.
