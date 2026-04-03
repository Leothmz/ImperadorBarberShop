import { test, expect } from '@playwright/test'

test.describe('Fluxo de agendamento', () => {
  test.beforeEach(async ({ page }) => {
    await page.context().clearCookies()
    await page.evaluate(() => localStorage.clear())
  })

  test('registra um cliente e realiza um agendamento completo', async ({ page }) => {
    const timestamp = Date.now()
    const email = `cliente${timestamp}@teste.com`

    // ─── Cadastro ────────────────────────────────────────────────────
    await page.goto('/register/client')
    await expect(page.getByRole('heading', { name: /Crie sua conta/i })).toBeVisible()

    await page.getByLabel('Nome completo').fill('João Teste')
    await page.getByLabel('E-mail').fill(email)
    // Fill both password fields
    const passwordFields = page.getByLabel(/Senha/)
    await passwordFields.first().fill('senha123')
    await passwordFields.last().fill('senha123')

    await page.getByRole('button', { name: /Criar conta/i }).click()

    // After registration, should go to client dashboard
    await expect(page).toHaveURL(/\/client\/dashboard/, { timeout: 10000 })

    // ─── Navegar para agendamento ─────────────────────────────────────
    await page.goto('/client/book')
    await expect(page.getByRole('heading', { name: /Novo Agendamento/i })).toBeVisible()

    // ─── Passo 1: Escolher barbeiro ───────────────────────────────────
    await expect(page.getByText(/Escolha o Barbeiro/i)).toBeVisible()

    // Wait for barbers to load and select first one
    const barberCards = page.getByRole('listitem')
    await expect(barberCards.first()).toBeVisible({ timeout: 10000 })
    await barberCards.first().click()

    // Advance to step 2
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 2: Escolher serviços ───────────────────────────────────
    await expect(page.getByText(/Escolha os Serviços/i)).toBeVisible()

    // Wait for services to load and select the first one
    const serviceCheckboxes = page.locator('input[type="checkbox"]')
    await expect(serviceCheckboxes.first()).toBeVisible({ timeout: 10000 })
    await serviceCheckboxes.first().click()

    // Check total is shown
    await expect(page.getByLabelText(/Total dos serviços selecionados/i)).toBeVisible()

    // Advance to step 3
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 3: Escolher data e horário ─────────────────────────────
    await expect(page.getByText(/Escolha Data e Horário/i)).toBeVisible()

    // Click on a future date (find a non-disabled date button)
    const dayButtons = page.locator('.rdp-root button:not([disabled])')
    await expect(dayButtons.first()).toBeVisible({ timeout: 10000 })
    await dayButtons.first().click()

    // Wait for time slots to load and select one
    const slotButtons = page.getByRole('option')
    await expect(slotButtons.first()).toBeVisible({ timeout: 10000 })
    await slotButtons.first().click()

    // Advance to step 4
    await page.getByRole('button', { name: /Próximo/i }).click()

    // ─── Passo 4: Confirmar ───────────────────────────────────────────
    await expect(page.getByText(/Confirmar Agendamento/i)).toBeVisible()
    await expect(page.getByText(/Resumo do agendamento/i)).toBeVisible()

    // Confirm the booking
    await page.getByRole('button', { name: /Confirmar Agendamento/i }).click()

    // Should redirect to dashboard after successful booking
    await expect(page).toHaveURL(/\/client\/dashboard/, { timeout: 15000 })
  })

  test('o botão Próximo fica desabilitado até um barbeiro ser selecionado', async ({ page }) => {
    // Login first
    await page.goto('/login')
    await page.getByLabel('E-mail').fill('teste@teste.com')
    await page.getByLabel('Senha').fill('senha123')
    await page.getByRole('button', { name: /Entrar/i }).click()
    await page.waitForURL(/\/dashboard/, { timeout: 10000 })

    await page.goto('/client/book')

    // The Next button should be disabled before selecting a barber
    const nextButton = page.getByRole('button', { name: /Próximo/i })
    await expect(nextButton).toBeDisabled()
  })
})
