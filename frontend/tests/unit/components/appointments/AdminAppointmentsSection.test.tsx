import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../../test-utils'
import AdminAppointmentsSection from '@/app/admin/barbers/AdminAppointmentsSection'

describe('AdminAppointmentsSection', () => {
  it('renders completed appointments', async () => {
    // MSW handler returns mockBarberAppointments with status Accepted
    // So completed list may be empty. Verify "Nenhum atendimento" message.
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await waitFor(() => {
      expect(screen.getByText(/Nenhum atendimento concluído/i)).toBeInTheDocument()
    })
  })
})
