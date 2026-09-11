import { defineConfig, devices } from '@playwright/test'

// O backend é semeado com estas credenciais pelo webServer abaixo; os specs leem
// as mesmas variáveis (E2E_ADMIN_*), então há uma única fonte para os dois lados.
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5044'
const APP_URL = process.env.E2E_BASE_URL ?? 'http://localhost:3000'
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@imperadorbarber.com.br'
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? 'admin12345'
const JWT_SECRET =
  process.env.E2E_JWT_SECRET ?? 'e2e-secret-e2e-secret-e2e-secret-e2e-secret'

process.env.E2E_API_URL = API_URL
process.env.E2E_ADMIN_EMAIL = ADMIN_EMAIL
process.env.E2E_ADMIN_PASSWORD = ADMIN_PASSWORD

// Banco novo por execução: o seed de admin e as fixtures de cada spec partem de
// um estado limpo, sem herdar barbeiros nem rate limits de uma rodada anterior.
const DB_PATH = `/tmp/imperador-e2e-${Date.now()}.db`

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  // O `next dev` compila a rota na primeira visita; 30 s é apertado para o
  // primeiro carregamento de cada rota em um runner frio.
  timeout: 60000,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'html',
  use: {
    baseURL: APP_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command:
        'dotnet run --project ../backend/src/Api/ImperadorBarberShop.Api --launch-profile http',
      url: `${API_URL}/api/v1/services`,
      reuseExistingServer: !process.env.CI,
      timeout: 240000,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__DefaultConnection: `Data Source=${DB_PATH}`,
        FrontendUrl: APP_URL,
        Jwt__Secret: JWT_SECRET,
        Jwt__Issuer: 'ImperadorBarberShop',
        Jwt__Audience: 'ImperadorBarberShopFrontend',
        Jwt__ExpirationMinutes: '15',
        Admin__Email: ADMIN_EMAIL,
        Admin__Password: ADMIN_PASSWORD,
        NOTIFICATIONS__CHANNELS: 'whatsapp',
      },
    },
    {
      command: 'npm run dev',
      url: APP_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 180000,
      env: {
        ...process.env,
        NEXT_PUBLIC_API_URL: API_URL,
      },
    },
  ],
})
