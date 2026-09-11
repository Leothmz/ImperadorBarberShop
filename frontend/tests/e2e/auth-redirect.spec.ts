import { test, expect } from '@playwright/test'

test.describe('Acesso público e proteção das áreas restritas', () => {
  test('a landing page é pública', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: /IMPERADOR/i })).toBeVisible()
  })

  test('a página de agendamento é pública', async ({ page }) => {
    await page.goto('/agendar')
    await expect(page).toHaveURL(/\/agendar/)
    await expect(page.getByRole('heading', { name: 'Agende seu horário' })).toBeVisible()
  })

  test('a página de login é acessível e nomeia o formulário', async ({ page }) => {
    await page.goto('/login')
    await expect(page.getByRole('heading', { name: /Bem-vindo de volta/i })).toBeVisible()
    await expect(page.getByLabel('E-mail')).toBeVisible()
    await expect(page.getByLabel('Senha', { exact: true })).toBeVisible()
  })

  test('sem sessão, /barber/dashboard redireciona para o login', async ({ page }) => {
    await page.goto('/barber/dashboard')
    await expect(page).toHaveURL(/\/login\?redirect=%2Fbarber%2Fdashboard/)
  })

  test('sem sessão, /admin/dashboard redireciona para o login', async ({ page }) => {
    await page.goto('/admin/dashboard')
    await expect(page).toHaveURL(/\/login\?redirect=%2Fadmin%2Fdashboard/)
  })
})
