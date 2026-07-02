import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import WhatsAppPage from '@/app/admin/whatsapp/page'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('WhatsAppPage — Conexão tab', () => {
  beforeEach(() => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'connected', phoneNumber: '+5511999990001' })
      ),
      http.get('*/admin/whatsapp/qr', () =>
        HttpResponse.json({ qrCode: 'data:image/png;base64,fake' })
      ),
      http.get('*/admin/notifications/settings', () =>
        HttpResponse.json({ channels: ['email', 'whatsapp'], reminderMinutesBefore: 60, notificationPhone: null })
      )
    )
  })

  it('renders Conexão and Notificações tabs', () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(screen.getByRole('button', { name: 'Conexão' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Notificações' })).toBeInTheDocument()
  })

  it('shows connected status badge', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByText('Conectado')).toBeInTheDocument()
    expect(screen.getByText('+5511999990001')).toBeInTheDocument()
  })

  it('shows disconnect button when connected', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByRole('button', { name: /desconectar/i })).toBeInTheDocument()
  })

  it('shows QR code when status is qr_required', async () => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'qr_required', phoneNumber: null })
      )
    )
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByAltText('QR Code WhatsApp')).toBeInTheDocument()
  })

  it('shows disconnected status when disconnected', async () => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'disconnected', phoneNumber: null })
      )
    )
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    expect(await screen.findByText('Desconectado')).toBeInTheDocument()
  })
})

describe('WhatsAppPage — Notificações tab', () => {
  beforeEach(() => {
    server.use(
      http.get('*/admin/whatsapp/status', () =>
        HttpResponse.json({ status: 'disconnected', phoneNumber: null })
      ),
      http.get('*/admin/whatsapp/qr', () =>
        HttpResponse.json({ qrCode: 'data:image/png;base64,fake' })
      ),
      http.get('*/admin/notifications/settings', () =>
        HttpResponse.json({ channels: ['email', 'whatsapp'], reminderMinutesBefore: 60, notificationPhone: null })
      )
    )
  })

  it('renders notification settings form with save button and phone input', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    const tab = screen.getByRole('button', { name: 'Notificações' })
    tab.click()
    expect(await screen.findByRole('button', { name: /salvar/i })).toBeInTheDocument()
    expect(screen.getByPlaceholderText('+5511999990000')).toBeInTheDocument()
  })

  it('renders channel radio group with three options', async () => {
    render(<WhatsAppPage />, { wrapper: createWrapper() })
    const tab = screen.getByRole('button', { name: 'Notificações' })
    tab.click()
    expect(await screen.findByText('Apenas Email')).toBeInTheDocument()
    expect(screen.getByText('Apenas WhatsApp')).toBeInTheDocument()
    expect(screen.getByText('Email e WhatsApp')).toBeInTheDocument()
    // Three radio buttons: email, whatsapp, email+whatsapp
    const radios = await screen.findAllByRole('radio')
    expect(radios).toHaveLength(3)
  })
})
