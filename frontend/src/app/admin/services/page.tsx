'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import Image from 'next/image'
import {
  useAdminAllServices,
  useCreateService,
  useDeactivateService,
  useActivateService,
  useAddAddon,
  useRemoveAddon,
} from '@/hooks/useAdminServices'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Spinner } from '@/components/ui/Spinner'
import type { Service } from '@/types/api.types'

const createServiceSchema = z.object({
  name: z.string().min(2, 'Nome deve ter pelo menos 2 caracteres'),
  description: z.string().min(5, 'Descrição deve ter pelo menos 5 caracteres'),
  price: z.coerce
    .number({ invalid_type_error: 'Preço inválido' })
    .positive('Preço deve ser positivo'),
  durationMinutes: z.coerce
    .number({ invalid_type_error: 'Duração inválida' })
    .int()
    .positive('Duração deve ser positiva'),
  photo: z.instanceof(File).optional(),
})

type CreateServiceFormData = z.infer<typeof createServiceSchema>

function CreateServiceForm({ onSuccess }: { onSuccess: () => void }) {
  const createService = useCreateService()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<CreateServiceFormData>({
    resolver: zodResolver(createServiceSchema),
  })

  async function onSubmit(data: CreateServiceFormData) {
    setServerError(null)
    try {
      await createService.mutateAsync({
        name: data.name,
        description: data.description,
        price: data.price,
        durationMinutes: data.durationMinutes,
        photo: data.photo,
      })
      onSuccess()
    } catch (err: unknown) {
      const axiosErr = err as {
        response?: { data?: { detail?: string; errors?: Record<string, string[]> } }
      }
      const res = axiosErr?.response?.data
      const message =
        (res?.errors ? Object.values(res.errors).flat().join(' ') : null) ??
        res?.detail ??
        'Erro ao criar serviço. Verifique os dados e tente novamente.'
      setServerError(message)
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <Input
        label="Nome"
        type="text"
        placeholder="Corte Clássico"
        error={errors.name?.message}
        {...register('name')}
      />
      <div className="flex flex-col gap-1">
        <label className="text-sm font-medium text-brand-white/80">Descrição</label>
        <textarea
          rows={3}
          placeholder="Descreva o serviço..."
          className={[
            'w-full rounded-md border bg-brand-black-soft px-3 py-2.5 text-brand-white placeholder:text-brand-white/30',
            'transition-colors duration-150 outline-none resize-none',
            'focus:border-brand-gold focus:ring-1 focus:ring-brand-gold',
            errors.description ? 'border-red-500' : 'border-brand-white/20',
          ].join(' ')}
          {...register('description')}
        />
        {errors.description && (
          <p role="alert" className="text-xs text-red-400">
            {errors.description.message}
          </p>
        )}
      </div>
      <div className="grid grid-cols-2 gap-4">
        <Input
          label="Preço (R$)"
          type="number"
          step="0.01"
          min="0"
          placeholder="35.00"
          error={errors.price?.message}
          {...register('price')}
        />
        <Input
          label="Duração (min)"
          type="number"
          min="1"
          placeholder="30"
          error={errors.durationMinutes?.message}
          {...register('durationMinutes')}
        />
      </div>

      {/* Photo upload */}
      <div className="flex flex-col gap-1">
        <label className="text-sm font-medium text-brand-white/80">
          Foto (opcional)
        </label>
        <input
          type="file"
          accept="image/*"
          className="text-sm text-brand-white/70 file:mr-4 file:py-1 file:px-3 file:rounded-md file:border-0 file:bg-brand-gold/20 file:text-brand-gold file:text-sm hover:file:bg-brand-gold/30"
          onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) setValue('photo', file)
          }}
        />
      </div>

      {serverError && (
        <p role="alert" className="text-sm text-red-400">
          {serverError}
        </p>
      )}

      <Button type="submit" isLoading={isSubmitting} className="mt-2 w-full">
        Criar serviço
      </Button>
    </form>
  )
}

function AddonsModal({
  service,
  allServices,
  onClose,
}: {
  service: Service
  allServices: Service[]
  onClose: () => void
}) {
  const addAddon = useAddAddon()
  const removeAddon = useRemoveAddon()
  const [error, setError] = useState<string | null>(null)

  // Use fresh data from allServices so the UI reflects the latest state after invalidation
  const currentService = allServices.find((s) => s.id === service.id) ?? service
  const currentAddonIds = new Set(currentService.addons.map((a) => a.id))
  const candidates = allServices.filter(
    (s) => s.id !== service.id && s.isActive
  )

  async function toggle(addonId: string, isCurrentlyLinked: boolean) {
    setError(null)
    try {
      if (isCurrentlyLinked) {
        await removeAddon.mutateAsync({ serviceId: service.id, addonId })
      } else {
        await addAddon.mutateAsync({ serviceId: service.id, addonId })
      }
    } catch {
      setError('Erro ao atualizar complemento. Tente novamente.')
    }
  }

  const isPending = addAddon.isPending || removeAddon.isPending

  return (
    <Modal isOpen onClose={onClose} title={`Complementos de "${service.name}"`}>
      <div className="flex flex-col gap-3 max-h-80 overflow-y-auto">
        {error && (
          <p role="alert" className="text-sm text-red-400">{error}</p>
        )}
        {candidates.length === 0 && (
          <p className="text-sm text-brand-white/50 text-center py-4">
            Nenhum outro serviço ativo disponível.
          </p>
        )}
        {candidates.map((candidate) => {
          const linked = currentAddonIds.has(candidate.id)
          return (
            <label
              key={candidate.id}
              className="flex items-center gap-3 cursor-pointer rounded-lg border border-brand-white/10 px-4 py-3 hover:bg-brand-white/5 transition-colors"
            >
              <input
                type="checkbox"
                checked={linked}
                disabled={isPending}
                onChange={() => toggle(candidate.id, linked)}
                className="h-4 w-4 rounded accent-brand-gold"
              />
              <div>
                <p className="text-sm font-medium text-brand-white">{candidate.name}</p>
                <p className="text-xs text-brand-white/50">
                  {candidate.durationMinutes} min ·{' '}
                  {candidate.price.toLocaleString('pt-BR', {
                    style: 'currency',
                    currency: 'BRL',
                  })}
                </p>
              </div>
            </label>
          )
        })}
      </div>
    </Modal>
  )
}

export default function ServicesPage() {
  const { data: services, isLoading } = useAdminAllServices()
  const deactivate = useDeactivateService()
  const activate = useActivateService()
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [addonsTarget, setAddonsTarget] = useState<Service | null>(null)

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="font-montserrat text-2xl font-black text-brand-white">
          Serviços
        </h1>
        <Button onClick={() => setShowCreateModal(true)}>Adicionar Serviço</Button>
      </div>

      <div className="overflow-x-auto rounded-xl border border-brand-white/10">
        <table className="w-full text-sm text-brand-white/80">
          <thead>
            <tr className="border-b border-brand-white/10 bg-brand-black-soft text-left text-brand-white/40">
              <th className="px-4 py-3">Serviço</th>
              <th className="px-4 py-3">Preço</th>
              <th className="px-4 py-3">Duração</th>
              <th className="px-4 py-3">Complementos</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Ações</th>
            </tr>
          </thead>
          <tbody>
            {services?.map((service) => (
              <tr
                key={service.id}
                className="border-b border-brand-white/5 hover:bg-brand-white/5 transition-colors"
              >
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    {service.photoUrl ? (
                      <Image
                        src={service.photoUrl}
                        alt={service.name}
                        width={36}
                        height={36}
                        className="rounded-md object-cover"
                      />
                    ) : (
                      <div className="h-9 w-9 rounded-md bg-brand-gold/10 flex items-center justify-center">
                        <span className="text-brand-gold text-lg">✂</span>
                      </div>
                    )}
                    <div>
                      <p className="font-medium text-brand-white">{service.name}</p>
                      <p className="text-xs text-brand-white/40 line-clamp-1">
                        {service.description}
                      </p>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3 text-brand-gold">
                  {service.price.toLocaleString('pt-BR', {
                    style: 'currency',
                    currency: 'BRL',
                  })}
                </td>
                <td className="px-4 py-3">{service.durationMinutes} min</td>
                <td className="px-4 py-3">
                  <button
                    onClick={() => setAddonsTarget(service)}
                    className="text-xs text-brand-gold hover:underline"
                  >
                    {service.addons.length} complemento{service.addons.length !== 1 ? 's' : ''}
                  </button>
                </td>
                <td className="px-4 py-3">
                  <span
                    className={[
                      'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold',
                      service.isActive
                        ? 'bg-green-500/20 text-green-400'
                        : 'bg-red-500/20 text-red-400',
                    ].join(' ')}
                  >
                    {service.isActive ? 'Ativo' : 'Inativo'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  {service.isActive ? (
                    <Button
                      variant="danger"
                      size="sm"
                      onClick={() => deactivate.mutate(service.id)}
                      isLoading={
                        deactivate.isPending && deactivate.variables === service.id
                      }
                    >
                      Desativar
                    </Button>
                  ) : (
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => activate.mutate(service.id)}
                      isLoading={
                        activate.isPending && activate.variables === service.id
                      }
                    >
                      Ativar
                    </Button>
                  )}
                </td>
              </tr>
            ))}
            {services?.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-brand-white/40">
                  Nenhum serviço cadastrado.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Modal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        title="Adicionar Serviço"
      >
        <CreateServiceForm onSuccess={() => setShowCreateModal(false)} />
      </Modal>

      {addonsTarget && (
        <AddonsModal
          service={addonsTarget}
          allServices={services ?? []}
          onClose={() => setAddonsTarget(null)}
        />
      )}
    </div>
  )
}
