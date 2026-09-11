import { test, expect } from '@playwright/test'
import {
  pickFirstSlotOfNextMonth,
  seedBarber,
  type SeededBarber,
} from './helpers'

test.describe('Agendamento do cliente anônimo', () => {
  let barber: SeededBarber

  test.beforeAll(async () => {
    barber = await seedBarber()
  })

  test('agenda sem login e cancela pelo link com token', async ({ page }) => {
    // O barbeiro vem da URL: a home já manda o cliente para cá com ?barbeiro=<id>.
    await page.goto(`/agendar?barbeiro=${barber.id}`)

    // Passo 2 — serviços
    await expect(page.getByText('Escolha os Serviços')).toBeVisible({ timeout: 15000 })
    await page.getByRole('checkbox').first().click()
    await page.getByRole('button', { name: 'Próximo' }).click()

    // Passo 3 — data e horário
    await expect(page.getByText('Escolha Data e Horário')).toBeVisible()
    await pickFirstSlotOfNextMonth(page)
    await page.getByRole('button', { name: 'Próximo' }).click()

    // Passo 4 — contato: nome + WhatsApp, e nada de conta.
    await expect(page.getByText('Confirmar Agendamento')).toBeVisible()
    await page.getByLabel('Nome completo').fill('Cliente E2E')
    await page.getByLabel('WhatsApp').fill(`11${String(Date.now()).slice(-9)}`)
    await page.getByRole('button', { name: 'Confirmar Agendamento' }).click()

    // O agendamento é criado e redireciona para o link opaco do cliente.
    await expect(page).toHaveURL(/\/agendamento\/[^/?]+/, { timeout: 15000 })
    await expect(
      page.getByRole('heading', { name: 'Agendamento confirmado' }),
    ).toBeVisible()

    // O mesmo link permite cancelar.
    await page.getByRole('button', { name: 'Cancelar agendamento' }).click()
    await expect(
      page.getByRole('heading', { name: 'Cancelar agendamento?' }),
    ).toBeVisible()
    await page.getByRole('button', { name: 'Sim, cancelar' }).click()

    await expect(page.getByText('Este agendamento foi cancelado.')).toBeVisible({
      timeout: 15000,
    })
  })

  test('o botão Próximo fica desabilitado até um barbeiro ser selecionado', async ({
    page,
  }) => {
    await page.goto('/agendar')
    await expect(page.getByRole('button', { name: 'Próximo' })).toBeDisabled()
  })
})
