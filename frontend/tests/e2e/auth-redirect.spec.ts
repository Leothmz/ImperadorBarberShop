import { test, expect } from '@playwright/test'

test.describe('Redirecionamento de autenticação', () => {
  test('redireciona para /login ao visitar /client/dashboard sem autenticação', async ({ page }) => {
    // Clear any existing cookies/storage
    await page.context().clearCookies()

    await page.goto('/client/dashboard')

    // Should be redirected to /login
    await expect(page).toHaveURL(/\/login/)
  })

  test('redireciona para /login ao visitar /barber/dashboard sem autenticação', async ({ page }) => {
    await page.context().clearCookies()

    await page.goto('/barber/dashboard')

    await expect(page).toHaveURL(/\/login/)
  })

  test('a página de login é acessível sem autenticação', async ({ page }) => {
    await page.goto('/login')
    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByRole('heading', { name: /Bem-vindo/i })).toBeVisible()
  })

  test('a landing page é acessível sem autenticação', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL('/')
    await expect(page.getByRole('heading', { name: /IMPERADOR/i })).toBeVisible()
  })

  test('redireciona com parâmetro de redirect na URL', async ({ page }) => {
    await page.context().clearCookies()

    await page.goto('/client/dashboard')

    await expect(page).toHaveURL(/redirect=%2Fclient%2Fdashboard/)
  })
})
