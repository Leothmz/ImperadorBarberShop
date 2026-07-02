'use client'

import { useState } from 'react'
import { useAdminBarberBlocks, useAdminCreateBarberBlock, useAdminDeleteBarberBlock } from '@/hooks/useAdminBlocks'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const DAYS = [
  { label: 'Dom', bit: 1 }, { label: 'Seg', bit: 2 }, { label: 'Ter', bit: 4 },
  { label: 'Qua', bit: 8 }, { label: 'Qui', bit: 16 }, { label: 'Sex', bit: 32 }, { label: 'Sáb', bit: 64 },
]

const blockSchema = z.object({
  startsAt: z.string().min(1),
  endsAt: z.string().min(1),
  description: z.string().max(200).optional(),
  isRecurring: z.boolean(),
  selectedDays: z.number().min(0).max(127).optional(),
  recurrenceEndsAt: z.string().optional(),
}).refine(d => d.endsAt > d.startsAt, { message: 'Fim deve ser após início', path: ['endsAt'] })
 .refine(d => !d.isRecurring || (d.selectedDays && d.selectedDays > 0), { message: 'Selecione ao menos um dia', path: ['selectedDays'] })

type FormValues = z.infer<typeof blockSchema>

export default function AdminBlocksSection({ barberId }: { barberId: string }) {
  const { data: blocks, isLoading } = useAdminBarberBlocks(barberId)
  const createBlock = useAdminCreateBarberBlock(barberId)
  const deleteBlock = useAdminDeleteBarberBlock(barberId)
  const [open, setOpen] = useState(false)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const { register, handleSubmit, watch, setValue, reset, formState: { errors } } =
    useForm<FormValues>({ resolver: zodResolver(blockSchema), defaultValues: { isRecurring: false, selectedDays: 0 } })

  const isRecurring = watch('isRecurring')
  const selectedDays = watch('selectedDays') ?? 0

  const onSubmit = async (data: FormValues) => {
    const payload: CreateBarberBlockPayload = {
      startsAt: new Date(data.startsAt).toISOString(),
      endsAt: new Date(data.endsAt).toISOString(),
      description: data.description || undefined,
      isRecurring: data.isRecurring,
      recurrenceDays: data.isRecurring ? (data.selectedDays ?? null) : null,
      recurrenceEndsAt: data.isRecurring && data.recurrenceEndsAt ? new Date(data.recurrenceEndsAt).toISOString() : null,
    }
    try {
      await createBlock.mutateAsync(payload)
      reset()
      setOpen(false)
    } catch {
      // error displayed via createBlock.error below
    }
  }

  const formatDate = (iso: string) => new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
  const bitsToLabels = (bits: number) => DAYS.filter(d => (bits & d.bit) !== 0).map(d => d.label).join(', ')

  const handleDelete = async (id: string) => {
    if (window.confirm('Excluir este bloqueio?')) {
      setDeletingId(id)
      try {
        await deleteBlock.mutateAsync(id)
      } finally {
        setDeletingId(null)
      }
    }
  }

  return (
    <div className="space-y-3 mt-4">
      <div className="flex justify-between items-center">
        <h3 className="text-brand-white font-medium">Bloqueios</h3>
        <Button onClick={() => setOpen(true)}>Adicionar Bloqueio</Button>
      </div>

      {isLoading && <p className="text-brand-white/60 text-sm">Carregando...</p>}
      {!isLoading && blocks?.length === 0 && <p className="text-brand-white/60 text-sm">Nenhum bloqueio.</p>}

      <ul className="space-y-2">
        {blocks?.map(block => (
          <li key={block.id} className="bg-brand-black rounded p-3 flex justify-between items-start">
            <div>
              <p className="text-brand-white text-sm">{formatDate(block.startsAt)} → {formatDate(block.endsAt)}</p>
              {block.description && <p className="text-brand-white/60 text-xs">{block.description}</p>}
              {block.isRecurring && block.recurrenceDays != null && (
                <p className="text-brand-gold text-xs">{bitsToLabels(block.recurrenceDays)}</p>
              )}
            </div>
            <button
              onClick={() => handleDelete(block.id)}
              disabled={deletingId === block.id}
              className="text-brand-white/50 hover:text-brand-white disabled:opacity-50 text-xs ml-2"
            >
              Excluir
            </button>
          </li>
        ))}
      </ul>

      <Modal isOpen={open} onClose={() => { setOpen(false); reset() }} title="Adicionar Bloqueio">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Início</label>
            <Input type="datetime-local" {...register('startsAt')} />
            {errors.startsAt && <p className="text-brand-gold/70 text-xs">{errors.startsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Fim</label>
            <Input type="datetime-local" {...register('endsAt')} />
            {errors.endsAt && <p className="text-brand-gold/70 text-xs">{errors.endsAt.message}</p>}
          </div>
          <div>
            <label className="block text-brand-white/70 text-sm mb-1">Descrição</label>
            <Input type="text" {...register('description')} />
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id={`rec-${barberId}`} {...register('isRecurring')} className="accent-brand-gold" />
            <label htmlFor={`rec-${barberId}`} className="text-brand-white text-sm">Recorrente</label>
          </div>
          {isRecurring && (
            <>
              <div className="flex gap-2 flex-wrap">
                {DAYS.map(d => (
                  <button key={d.bit} type="button"
                    onClick={() => setValue('selectedDays', selectedDays ^ d.bit)}
                    className={`px-2 py-1 rounded text-xs border ${(selectedDays & d.bit) !== 0 ? 'bg-brand-gold text-brand-black border-brand-gold' : 'text-brand-white/70 border-brand-white/20'}`}>
                    {d.label}
                  </button>
                ))}
              </div>
              {errors.selectedDays && <p className="text-brand-gold/70 text-xs">{errors.selectedDays.message}</p>}
              <Input type="date" {...register('recurrenceEndsAt')} placeholder="Repetir até (opcional)" />
            </>
          )}
          {createBlock.error && (
            <p className="text-brand-gold/70 text-sm">
              {(createBlock.error as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? 'Erro ao salvar bloqueio.'}
            </p>
          )}
          <Button type="submit" disabled={createBlock.isPending} className="w-full">
            {createBlock.isPending ? 'Salvando...' : 'Salvar'}
          </Button>
        </form>
      </Modal>
    </div>
  )
}
