import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../../test-utils'
import { ManageAppointmentView } from '@/app/agendamento/[token]/ManageAppointmentView'

describe('ManageAppointmentPage', () => {
  it('renders the appointment summary for a valid token', async () => {
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => {
      expect(screen.getByText('Carlos Andrade')).toBeInTheDocument()
      expect(screen.getByText('João Silva')).toBeInTheDocument()
    })
  })

  it('shows a cancel button for an Accepted appointment scheduled far in the future', async () => {
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Cancelar agendamento' })).toBeEnabled()
    })
  })
})
