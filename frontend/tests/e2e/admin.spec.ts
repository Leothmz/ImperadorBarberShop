import { test, expect } from '@playwright/test'
import { ADMIN_EMAIL, ADMIN_PASSWORD, loginViaUi, uniqueSuffix } from './helpers'

test.describe('Admin', () => {
  test('faz login e cadastra um barbeiro', async ({ page }) => {
    await loginViaUi(page, ADMIN_EMAIL, ADMIN_PASSWORD)
    await expect(page).toHaveURL(/\/admin\/dashboard/, { timeout: 15000 })

    // A navegação client-side preserva a sessão em memória — sem reload.
    await page.getByRole('link', { name: 'Barbeiros' }).click()
    await expect(page).toHaveURL(/\/admin\/barbers/)

    const suffix = uniqueSuffix()
    const name = `Barbeiro UI ${suffix}`
    const email = `barbeiro-ui-${suffix}@teste.com`

    await page.getByRole('button', { name: 'Adicionar Barbeiro' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog.getByRole('heading', { name: 'Adicionar Barbeiro' })).toBeVisible()

    await dialog.getByLabel('Nome completo').fill(name)
    await dialog.getByLabel('E-mail').fill(email)
    await dialog.getByLabel('Senha', { exact: true }).fill('senha12345')
    await dialog.getByLabel('Confirmar senha', { exact: true }).fill('senha12345')

    // A disponibilidade padrão já vem com segunda a sexta habilitados.
    await dialog.getByRole('button', { name: 'Criar barbeiro' }).click()

    // O modal fecha e a lista é invalidada: o barbeiro novo aparece.
    await expect(page.getByRole('dialog')).toBeHidden({ timeout: 15000 })
    await expect(page.getByText(name, { exact: true })).toBeVisible({ timeout: 15000 })
  })

  test('renderiza o dashboard financeiro', async ({ page }) => {
    await loginViaUi(page, ADMIN_EMAIL, ADMIN_PASSWORD)
    await expect(page).toHaveURL(/\/admin\/dashboard/, { timeout: 15000 })

    await expect(
      page.getByRole('heading', { name: 'Dashboard Financeiro' }),
    ).toBeVisible()
    await expect(page.getByText('Receita Total')).toBeVisible()
    await expect(page.getByText('Lucro Líquido')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Exportar CSV' })).toBeVisible()
  })
})
