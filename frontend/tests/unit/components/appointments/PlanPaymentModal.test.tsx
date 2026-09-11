import { describe, it, expect, vi } from 'vitest'
import userEvent from '@testing-library/user-event'
import { render, screen } from '@testing-library/react'
import { PlanPaymentModal } from '@/components/appointments/PlanPaymentModal'

function setup(onConfirm = vi.fn().mockResolvedValue(undefined), action: 'complete' | 'register' = 'complete') {
  const onClose = vi.fn()
  render(<PlanPaymentModal clientName="Pedro Costa" action={action} onClose={onClose} onConfirm={onConfirm} />)
  return { onConfirm, onClose, user: userEvent.setup() }
}

describe('PlanPaymentModal', () => {
  it('first asks whether it is a plan payment or a recurrence', () => {
    setup()

    expect(screen.getByRole('dialog')).toHaveTextContent('Pedro Costa')
    expect(screen.getByRole('heading', { name: 'Plano' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Pagamento/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Recorrência/ })).toBeInTheDocument()
  })

  it('Pagamento asks for the amount and the tender, and sends the full plan payload', async () => {
    const { onConfirm, user } = setup()

    await user.click(screen.getByRole('button', { name: /^Pagamento/ }))
    expect(screen.getByRole('heading', { name: 'Pagamento do plano' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Valor cobrado (R$)'), '120,50')
    await user.click(screen.getByRole('button', { name: /Cartão/ }))
    await user.click(screen.getByRole('button', { name: 'Concluir atendimento' }))

    expect(onConfirm).toHaveBeenCalledWith({
      paymentMethod: 'Plano',
      planKind: 'Pagamento',
      planTender: 'Cartão',
      chargedAmount: 120.5,
    })
  })

  it('offers exactly one generic Cartão, next to Pix and Dinheiro', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: /^Pagamento/ }))

    const tenders = screen.getAllByRole('button', { pressed: false }).map((b) => b.textContent?.trim())
    expect(tenders).toEqual(['⚡ Pix', '💵 Dinheiro', '💳 Cartão'])
  })

  it('cannot be submitted until both the amount and the tender are valid', async () => {
    const { onConfirm, user } = setup()
    await user.click(screen.getByRole('button', { name: /^Pagamento/ }))
    const confirm = screen.getByRole('button', { name: 'Concluir atendimento' })

    expect(confirm).toBeDisabled()
    await user.type(screen.getByLabelText('Valor cobrado (R$)'), '80')
    expect(confirm).toBeDisabled()
    await user.click(screen.getByRole('button', { name: /Pix/ }))
    expect(confirm).toBeEnabled()

    await user.clear(screen.getByLabelText('Valor cobrado (R$)'))
    expect(confirm).toBeDisabled()
    await user.type(screen.getByLabelText('Valor cobrado (R$)'), '{Enter}')
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('shows why a negative amount or extra decimals are refused', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: /^Pagamento/ }))
    await user.click(screen.getByRole('button', { name: /Pix/ }))
    const amount = screen.getByLabelText('Valor cobrado (R$)')

    await user.type(amount, '-10')
    expect(screen.getByRole('alert')).toHaveTextContent('O valor não pode ser negativo.')
    expect(screen.getByRole('button', { name: 'Concluir atendimento' })).toBeDisabled()

    await user.clear(amount)
    await user.type(amount, '10,555')
    expect(screen.getByRole('alert')).toHaveTextContent('Use no máximo duas casas decimais.')
    expect(screen.getByRole('button', { name: 'Concluir atendimento' })).toBeDisabled()
  })

  it('accepts zero as a plan payment amount', async () => {
    const { onConfirm, user } = setup()
    await user.click(screen.getByRole('button', { name: /^Pagamento/ }))
    await user.type(screen.getByLabelText('Valor cobrado (R$)'), '0')
    await user.click(screen.getByRole('button', { name: /Dinheiro/ }))
    await user.click(screen.getByRole('button', { name: 'Concluir atendimento' }))

    expect(onConfirm).toHaveBeenCalledWith(
      expect.objectContaining({ planKind: 'Pagamento', planTender: 'Dinheiro', chargedAmount: 0 })
    )
  })

  it('Recorrência confirms the zero amount without asking for a tender', async () => {
    const { onConfirm, user } = setup(undefined, 'register')

    await user.click(screen.getByRole('button', { name: /^Recorrência/ }))
    expect(screen.getByRole('heading', { name: 'Recorrência do plano' })).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toHaveTextContent(/R\$\s*0,00/)
    expect(screen.queryByLabelText('Valor cobrado (R$)')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Pix/ })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Registrar pagamento' }))

    expect(onConfirm).toHaveBeenCalledWith({ paymentMethod: 'Plano', planKind: 'Recorrencia' })
  })

  it('Voltar goes back to the plan kind question', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: /^Recorrência/ }))
    await user.click(screen.getByRole('button', { name: 'Voltar' }))

    expect(screen.getByRole('heading', { name: 'Plano' })).toBeInTheDocument()
  })

  it('Cancelar closes without sending anything', async () => {
    const { onConfirm, onClose, user } = setup()
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onClose).toHaveBeenCalled()
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('stays open with the API validation message when the save fails', async () => {
    const onConfirm = vi.fn().mockRejectedValue({
      response: { status: 400, data: { errors: { ChargedAmount: ['O valor cobrado não pode ser negativo.'] } } },
    })
    const { user } = setup(onConfirm)
    await user.click(screen.getByRole('button', { name: /^Recorrência/ }))
    await user.click(screen.getByRole('button', { name: 'Concluir atendimento' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('O valor cobrado não pode ser negativo.')
    expect(screen.getByRole('button', { name: 'Concluir atendimento' })).toBeEnabled()
  })
})
