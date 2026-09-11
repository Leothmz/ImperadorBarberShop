import { expect, request, type Page } from '@playwright/test'

/**
 * End-to-end helpers.
 *
 * The specs drive the real application (Next.js on :3000 + ASP.NET Core on
 * :5044). Staff accounts still need to exist before a browser can log in, so the
 * few fixtures a journey depends on are seeded through the public HTTP API before
 * the test uses the real UI — the seeding is setup, never the thing under test.
 */

export const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5044'

export const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@imperadorbarber.com.br'
export const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? 'admin12345'

let counter = 0

/** Run-unique suffix so repeated runs never collide on email/phone. */
export function uniqueSuffix(): string {
  counter += 1
  return `${Date.now()}-${counter}`
}

export interface SeededBarber {
  id: string
  name: string
  email: string
  password: string
}

interface AvailabilitySlot {
  dayOfWeek: string
  startTime: string
  endTime: string
}

/**
 * Every weekday, 08:00–18:00. Used when a test needs a bookable barber without
 * caring about the day of the week the suite happens to run on.
 */
export const ALL_WEEK_AVAILABILITY: AvailabilitySlot[] = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
].map((dayOfWeek) => ({ dayOfWeek, startTime: '08:00:00', endTime: '18:00:00' }))

async function loginAdminViaApi(): Promise<string> {
  const api = await request.newContext()
  try {
    const response = await api.post(`${API_URL}/api/v1/auth/login`, {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    })
    if (!response.ok()) {
      throw new Error(
        `Admin API login failed (${response.status()}). ` +
          `Ensure the backend sits at ${API_URL} with Admin__Email=${ADMIN_EMAIL}. ` +
          `Body: ${await response.text()}`,
      )
    }
    const body = (await response.json()) as { accessToken: string }
    return body.accessToken
  } finally {
    await api.dispose()
  }
}

/** Creates a barber through the admin API and returns the credentials to log in with. */
export async function seedBarber(
  overrides: Partial<SeededBarber> = {},
): Promise<SeededBarber> {
  const suffix = uniqueSuffix()
  const barber: SeededBarber = {
    id: '',
    name: overrides.name ?? `Barbeiro E2E ${suffix}`,
    email: overrides.email ?? `barbeiro-e2e-${suffix}@teste.com`,
    password: overrides.password ?? 'senha12345',
  }

  const token = await loginAdminViaApi()
  const api = await request.newContext()
  try {
    const multipart: Record<string, string> = {
      name: barber.name,
      email: barber.email,
      password: barber.password,
    }
    ALL_WEEK_AVAILABILITY.forEach((slot, index) => {
      multipart[`availability[${index}].dayOfWeek`] = slot.dayOfWeek
      multipart[`availability[${index}].startTime`] = slot.startTime
      multipart[`availability[${index}].endTime`] = slot.endTime
    })

    const response = await api.post(`${API_URL}/api/v1/admin/barbers`, {
      headers: { Authorization: `Bearer ${token}` },
      multipart,
    })
    if (!response.ok()) {
      throw new Error(
        `Seeding barber failed (${response.status()}). Body: ${await response.text()}`,
      )
    }

    // Create returns the *user* id; the barber aggregate gets its own id, and
    // that is what the public /agendar?barbeiro=<id> link needs.
    const list = await api.get(`${API_URL}/api/v1/barbers`)
    const created = ((await list.json()) as Array<{ id: string; email: string }>).find(
      (candidate) => candidate.email === barber.email,
    )
    if (!created) {
      throw new Error(`Seeded barber ${barber.email} did not appear in GET /barbers`)
    }
    barber.id = created.id
    return barber
  } finally {
    await api.dispose()
  }
}

/** Logs in through the real login form. The caller asserts the landing route. */
export async function loginViaUi(
  page: Page,
  email: string,
  password: string,
): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Senha', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Entrar' }).click()
}

/**
 * Advances the booking calendar to the next month and picks its first enabled
 * day. Next month is always in the future, so the selected day never depends on
 * the time of day the suite runs at (the shop clock is America/Sao_Paulo).
 */
export async function pickFirstSlotOfNextMonth(page: Page): Promise<void> {
  const today = new Date()
  const nextMonth = new Date(today.getFullYear(), today.getMonth() + 1, 1)
  const nextMonthPrefix = `${nextMonth.getFullYear()}-${String(
    nextMonth.getMonth() + 1,
  ).padStart(2, '0')}`

  await expect(page.locator('.rdp-root')).toBeVisible()
  await page.locator('.rdp-root nav button').last().click()

  // Anchor on the ISO date instead of "first cell": this survives the month
  // change re-render and rejects any outside day the grid may still show.
  const firstAvailableDay = page
    .locator(
      `.rdp-root td[data-day^="${nextMonthPrefix}"]:has(button:not([disabled]))`,
    )
    .first()
  await expect(firstAvailableDay).toBeVisible()
  await firstAvailableDay.locator('button').click()

  const firstSlot = page.getByRole('option').first()
  await expect(firstSlot).toBeVisible()
  await firstSlot.click()
}
