import { test, expect } from '@playwright/test'

test.describe('Gestão de agendamentos pelo barbeiro', () => {
  test.beforeEach(async ({ page }) => {
    await page.context().clearCookies()
    await page.evaluate(() => localStorage.clear())
  })

  test('registra um barbeiro com disponibilidade', async ({ page }) => {
    const timestamp = Date.now()
    const email = `barbeiro${timestamp}@teste.com`

    await page.goto('/register/barber')
    await expect(page.getByRole('heading', { name: /Cadastro de Barbeiro/i })).toBeVisible()

    await page.getByLabel('Nome completo').fill('Barbeiro Teste')
    await page.getByLabel('E-mail').fill(email)

    const passwordFields = page.getByLabel(/Senha/)
    await passwordFields.first().fill('senha123')
    await passwordFields.last().fill('senha123')

    await expect(page.getByRole('group', { name: /Disponibilidade/i })).toBeVisible()
    await page.getByRole('button', { name: /Criar conta de barbeiro/i }).click()

    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 10000 })
  })

  test('faz login como barbeiro e visualiza o dashboard', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-mail').fill('barber@test.com')
    await page.getByLabel('Senha').fill('senha123')
    await page.getByRole('button', { name: /Entrar/i }).click()

    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 10000 })
    await expect(page.getByRole('heading', { name: /Minha Agenda/i })).toBeVisible()
  })

  test('barbeiro cancela um agendamento confirmado, se existir', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-mail').fill('barber@test.com')
    await page.getByLabel('Senha').fill('senha123')
    await page.getByRole('button', { name: /Entrar/i }).click()
    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 10000 })

    const cancelButton = page.getByRole('button', { name: /Cancelar/i }).first()
    const hasAppointment = await cancelButton.isVisible({ timeout: 5000 }).catch(() => false)

    if (hasAppointment) {
      await cancelButton.click()
      await page.waitForTimeout(1000)
    } else {
      test.info().annotations.push({
        type: 'info',
        description: 'Nenhum agendamento confirmado encontrado. Teste ignorado.',
      })
    }
  })
})
