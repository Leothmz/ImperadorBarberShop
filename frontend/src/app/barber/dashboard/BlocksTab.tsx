'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useBarberBlocks, useCreateBarberBlock, useDeleteBarberBlock } from '@/hooks/useBarberBlocks'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'
import type { CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const DAYS = [
  { label: 'Dom', bit: 1 },
  { label: 'Seg', bit: 2 },
  { label: 'Ter', bit: 4 },
  { label: 'Qua', bit: 8 },
  { label: 'Qui', bit: 16 },
  { label: 'Sex', bit: 32 },
  { label: 'Sáb', bit: 64 },
]

const blockSchema = z.object({
  startsAt: z.string().min(1, 'Obrigatório'),
  endsAt: z.string().min(1, 'Obrigatório'),
  description: z.string().max(200).optional(),
  isRecurring: z.boolean(),
  selectedDays: z.number().min(0).max(127).optional(),
  recurrenceEndsAt: z.string().optional(),
}).refine(d => d.endsAt > d.startsAt, {
  message: 'Fim deve ser após início',
  path: ['endsAt'],
}).refine(d => !d.isRecurring || (d.selectedDays && d.selectedDays > 0), {
  message: 'Selecione ao menos um dia',
  path: ['selectedDays'],
})

type BlockFormValues = z.infer<typeof blockSchema>

export default function BlocksTab() {
  const { data: blocks, isLoading } = useBarberBlocks()
  const createBlock = useCreateBarberBlock()
  const deleteBlock = useDeleteBarberBlock()
  const [open, setOpen] = useState(false)

  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } =
    useForm<BlockFormValues>({
      resolver: zodResolver(blockSchema),
      defaultValues: { isRecurring: false, selectedDays: 0 },
    })

  const isRecurring = watch('isRecurring')
  const selectedDays = watch('selectedDays') ?? 0

  const toggleDay = (bit: number) => {
    setValue('selectedDays', selectedDays ^ bit)
  }

  const onSubmit = async (data: BlockFormValues) => {
    const payload: CreateBarberBlockPayload = {
      startsAt: new Date(data.startsAt).toISOString(),
      endsAt: new Date(data.endsAt).toISOString(),
      description: data.description || undefined,
      isRecurring: data.isRecurring,
      recurrenceDays: data.isRecurring ? (data.selectedDays ?? null) : null,
      recurrenceEndsAt: data.isRecurring && data.recurrenceEndsAt
        ? new Date(data.recurrenceEndsAt).toISOString()
        : null,
    }
    await createBlock.mutateAsync(payload)
    reset()
    setOpen(false)
  }

  const formatDate = (iso: string) =>
    new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

  const bitsToLabels = (bits: number) =>
    DAYS.filter(d => (bits & d.bit) !== 0).map(d => d.label).join(', ')

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h2 className="text-lg font-semibold text-brand-white">Bloqueios</h2>
        <Button onClick={() => setOpen(true)}>Adicionar Bloqueio</Button>
      </div>

      {isLoading && <p className="text-brand-white/60">Carregando...</p>}

      {blocks?.length === 0 && (
        <p className="text-brand-white/60">Nenhum bloqueio cadastrado.</p>
      )}

      <ul className="space-y-2">
        {blocks?.map(block => (
          <li key={block.id} className="bg-brand-black-soft rounded-lg p-4 flex justify-between items-start">
            <div>
              <p className="text-brand-white font-medium">
                {formatDate(block.startsAt)} → {formatDate(block.endsAt)}
              </p>
              {block.description && (
                <p className="text-brand-white/70 text-sm">{block.description}</p>
              )}
              {block.isRecurring && block.recurrenceDays != null && (
                <p className="text-brand-gold text-xs mt-1">
                  Recorrente: {bitsToLabels(block.recurrenceDays)}
                  {block.recurrenceEndsAt && ` até ${formatDate(block.recurrenceEndsAt)}`}
                </p>
              )}
            </div>
            <button
              onClick={() => {
                if (window.confirm('Excluir este bloqueio?')) {
                  deleteBlock.mutate(block.id)
                }
              }}
              disabled={deleteBlock.isPending}
              className="text-brand-white/50 hover:text-brand-white text-sm ml-4 disabled:opacity-40"
            >
              Excluir
            </button>
          </li>
        ))}
      </ul>

      <Modal isOpen={open} onClose={() => { setOpen(false); reset() }} title="Adicionar Bloqueio">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Início</label>
            <Input type="datetime-local" {...register('startsAt')} />
            {errors.startsAt && <p className="text-red-400 text-xs">{errors.startsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Fim</label>
            <Input type="datetime-local" {...register('endsAt')} />
            {errors.endsAt && <p className="text-red-400 text-xs">{errors.endsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Descrição (opcional)</label>
            <Input type="text" placeholder="Ex: Almoço, Folga..." {...register('description')} />
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="isRecurring" {...register('isRecurring')} className="accent-brand-gold" />
            <label htmlFor="isRecurring" className="text-brand-white text-sm">Recorrente</label>
          </div>

          {isRecurring && (
            <>
              <div>
                <p className="text-brand-white/70 text-sm mb-2">Dias da semana</p>
                <div className="flex gap-2 flex-wrap">
                  {DAYS.map(d => (
                    <button
                      key={d.bit}
                      type="button"
                      onClick={() => toggleDay(d.bit)}
                      className={`px-3 py-1 rounded text-sm font-medium border transition-colors ${
                        (selectedDays & d.bit) !== 0
                          ? 'bg-brand-gold text-brand-black border-brand-gold'
                          : 'bg-transparent text-brand-white/70 border-brand-white/20'
                      }`}
                    >
                      {d.label}
                    </button>
                  ))}
                </div>
                {errors.selectedDays && <p className="text-red-400 text-xs mt-1">{errors.selectedDays.message}</p>}
              </div>
              <div>
                <label className="block text-brand-white/70 text-sm mb-1">Repetir até (opcional)</label>
                <Input type="date" {...register('recurrenceEndsAt')} />
              </div>
            </>
          )}

          <Button type="submit" disabled={createBlock.isPending} className="w-full">
            {createBlock.isPending ? 'Salvando...' : 'Salvar'}
          </Button>

          {createBlock.isError && (
            <p className="text-red-400 text-sm">Erro ao salvar. Tente novamente.</p>
          )}
        </form>
      </Modal>
    </div>
  )
}
