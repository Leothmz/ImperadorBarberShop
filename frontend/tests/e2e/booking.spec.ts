import { test, expect } from '@playwright/test'

test.describe('Fluxo de agendamento', () => {
  test('realiza um agendamento completo sem login', async ({ page }) => {
    await page.goto('/agendar')
    await expect(page.getByRole('heading', { name: /Novo Agendamento/i })).toBeVisible()

    // ─── Passo 1: Escolher barbeiro ───────────────────────────────────
    await expect(page.getByText(/Escolha o Barbeiro/i)).toBeVisible()
    const barberCards = page.getByRole('listitem')
    await expect(barberCards.first()).toBeVisible({ timeout: 10000 })
    await barberCards.first().click()
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 2: Escolher serviços ───────────────────────────────────
    await expect(page.getByText(/Escolha os Serviços/i)).toBeVisible()
    const serviceCheckboxes = page.locator('input[type="checkbox"]')
    await expect(serviceCheckboxes.first()).toBeVisible({ timeout: 10000 })
    await serviceCheckboxes.first().click()
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 3: Escolher data e horário ─────────────────────────────
    await expect(page.getByText(/Escolha Data e Horário/i)).toBeVisible()
    const dayButtons = page.locator('.rdp-root button:not([disabled])')
    await expect(dayButtons.first()).toBeVisible({ timeout: 10000 })
    await dayButtons.first().click()
    const slotButtons = page.getByRole('option')
    await expect(slotButtons.first()).toBeVisible({ timeout: 10000 })
    await slotButtons.first().click()
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 4: Confirmar ───────────────────────────────────────────
    await expect(page.getByText(/Confirmar Agendamento/i)).toBeVisible()
    await page.getByLabel('Nome completo').fill('João Teste')
    await page.getByLabel('WhatsApp').fill('11999990000')

    await page.getByRole('button', { name: /Confirmar Agendamento/i }).click()

    // Redirects to the public management page, keyed by the access token
    await expect(page).toHaveURL(/\/agendamento\/.+/, { timeout: 15000 })
  })

  test('o botão Próximo fica desabilitado até um barbeiro ser selecionado', async ({ page }) => {
    await page.goto('/agendar')

    const nextButton = page.getByRole('button', { name: /Próximo/i })
    await expect(nextButton).toBeDisabled()
  })
})
