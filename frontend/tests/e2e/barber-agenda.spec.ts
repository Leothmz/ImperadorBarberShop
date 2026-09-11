import { test, expect } from '@playwright/test'
import { loginViaUi, seedBarber, type SeededBarber } from './helpers'

test.describe('Barbeiro', () => {
  let barber: SeededBarber

  test.beforeAll(async () => {
    barber = await seedBarber()
  })

  test('faz login e vê a própria agenda do dia', async ({ page }) => {
    await loginViaUi(page, barber.email, barber.password)
    await expect(page).toHaveURL(/\/barber\/dashboard/, { timeout: 15000 })

    await expect(page.getByRole('heading', { name: 'Minha Agenda' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Agendamentos' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Bloqueios' })).toBeVisible()

    // A agenda carrega da API: sem atendimentos, o estado vazio é a resposta real.
    await expect(page.getByText('Nenhum agendamento encontrado.')).toBeVisible({
      timeout: 15000,
    })
  })
})
