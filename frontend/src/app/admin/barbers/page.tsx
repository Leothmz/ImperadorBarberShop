'use client'

import { useState } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import Image from 'next/image'
import {
  useAdminBarbers,
  useCreateBarber,
  useUpdateBarber,
  useDeleteBarber,
  useDeactivateBarber,
  useActivateBarber,
} from '@/hooks/useAdminBarbers'
import AdminBlocksSection from './AdminBlocksSection'
import AdminAppointmentsSection from './AdminAppointmentsSection'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Spinner } from '@/components/ui/Spinner'
import type { AdminBarber, DayOfWeekString } from '@/types/api.types'

const DAY_OF_WEEK_STRINGS: Record<number, DayOfWeekString> = {
  0: 'Sunday',
  1: 'Monday',
  2: 'Tuesday',
  3: 'Wednesday',
  4: 'Thursday',
  5: 'Friday',
  6: 'Saturday',
}

const DAY_OF_WEEK_NUMS: Record<DayOfWeekString, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
}

const WEEKDAYS = [
  { label: 'Domingo', value: 0 },
  { label: 'Segunda', value: 1 },
  { label: 'Terça', value: 2 },
  { label: 'Quarta', value: 3 },
  { label: 'Quinta', value: 4 },
  { label: 'Sexta', value: 5 },
  { label: 'Sábado', value: 6 },
]

const availabilitySchema = z.object({
  dayOfWeek: z.number(),
  startTime: z.string().regex(/^\d{2}:\d{2}$/, 'Horário inválido'),
  endTime: z.string().regex(/^\d{2}:\d{2}$/, 'Horário inválido'),
  enabled: z.boolean(),
})

const createBarberSchema = z
  .object({
    name: z.string().min(2, 'Nome deve ter pelo menos 2 caracteres'),
    email: z.string().email('E-mail inválido'),
    password: z.string().min(8, 'Senha deve ter pelo menos 8 caracteres'),
    confirmPassword: z.string(),
    photo: z.instanceof(File).optional(),
    availability: z.array(availabilitySchema),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'As senhas não coincidem',
    path: ['confirmPassword'],
  })
  .refine((data) => data.availability.some((a) => a.enabled), {
    message: 'Selecione pelo menos um dia de disponibilidade',
    path: ['availability'],
  })

const editBarberSchema = z
  .object({
    name: z.string().min(2, 'Nome deve ter pelo menos 2 caracteres'),
    email: z.string().email('E-mail inválido'),
    password: z.string().optional(),
    confirmPassword: z.string().optional(),
    photo: z.instanceof(File).optional(),
    availability: z.array(availabilitySchema),
  })
  .refine(
    (data) => !data.password || !data.confirmPassword || data.password === data.confirmPassword,
    { message: 'As senhas não coincidem', path: ['confirmPassword'] }
  )
  .refine(
    (data) => !data.password || data.password.length >= 8,
    { message: 'Senha deve ter pelo menos 8 caracteres', path: ['password'] }
  )
  .refine((data) => data.availability.some((a) => a.enabled), {
    message: 'Selecione pelo menos um dia de disponibilidade',
    path: ['availability'],
  })

type CreateBarberFormData = z.infer<typeof createBarberSchema>
type EditBarberFormData = z.infer<typeof editBarberSchema>

function AvailabilityPicker({
  control,
  register,
  watch,
  errors,
}: {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  control: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  register: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  watch: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  errors: any
}) {
  const { fields } = useFieldArray({ control, name: 'availability' })
  const availabilityValues = watch('availability')

  return (
    <fieldset className="rounded-lg border border-brand-white/10 p-4">
      <legend className="px-1 text-sm font-medium text-brand-white/80">
        Disponibilidade semanal
      </legend>
      <div className="flex flex-col gap-3 mt-2">
        {fields.map((field, index) => {
          const day = WEEKDAYS[index]
          const isEnabled = availabilityValues[index]?.enabled

          return (
            <div key={field.id} className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-3">
              <Controller
                control={control}
                name={`availability.${index}.enabled`}
                render={({ field: { value, onChange } }) => (
                  <label className="flex cursor-pointer items-center gap-2 sm:w-24">
                    <input
                      type="checkbox"
                      checked={value}
                      onChange={onChange}
                      className="h-4 w-4 rounded accent-brand-gold"
                      aria-label={`Habilitar ${day.label}`}
                    />
                    <span className="text-sm text-brand-white/80">{day.label}</span>
                  </label>
                )}
              />
              <div
                className={[
                  'flex flex-1 items-center gap-2 transition-opacity',
                  isEnabled ? 'opacity-100' : 'opacity-30',
                ].join(' ')}
              >
                <label className="text-xs text-brand-white/50">Início</label>
                <input
                  type="time"
                  disabled={!isEnabled}
                  className="min-w-0 flex-1 rounded border border-brand-white/20 bg-brand-black-soft px-2 py-1 text-sm text-brand-white focus:border-brand-gold focus:outline-none sm:flex-none"
                  {...register(`availability.${index}.startTime`)}
                />
                <label className="text-xs text-brand-white/50">Fim</label>
                <input
                  type="time"
                  disabled={!isEnabled}
                  className="min-w-0 flex-1 rounded border border-brand-white/20 bg-brand-black-soft px-2 py-1 text-sm text-brand-white focus:border-brand-gold focus:outline-none sm:flex-none"
                  {...register(`availability.${index}.endTime`)}
                />
              </div>
            </div>
          )
        })}
      </div>
      {errors.availability && (
        <p role="alert" className="mt-2 text-xs text-red-400">
          {Array.isArray(errors.availability)
            ? 'Verifique os horários de disponibilidade'
            : (errors.availability as { message?: string })?.message}
        </p>
      )}
    </fieldset>
  )
}

function CreateBarberForm({ onSuccess }: { onSuccess: () => void }) {
  const createBarber = useCreateBarber()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<CreateBarberFormData>({
    resolver: zodResolver(createBarberSchema),
    defaultValues: {
      availability: WEEKDAYS.map((d) => ({
        dayOfWeek: d.value,
        startTime: '09:00',
        endTime: '18:00',
        enabled: d.value >= 1 && d.value <= 5,
      })),
    },
  })

  async function onSubmit(data: CreateBarberFormData) {
    setServerError(null)
    const availability = data.availability
      .filter((a) => a.enabled)
      .map((a) => ({
        dayOfWeek: DAY_OF_WEEK_STRINGS[a.dayOfWeek],
        startTime: `${a.startTime}:00`,
        endTime: `${a.endTime}:00`,
      }))

    try {
      await createBarber.mutateAsync({
        name: data.name,
        email: data.email,
        password: data.password,
        availability,
        photo: data.photo,
      })
      onSuccess()
    } catch (err: unknown) {
      const axiosErr = err as {
        response?: { data?: { detail?: string; error?: string; errors?: Record<string, string[]> } }
      }
      const res = axiosErr?.response?.data
      const message =
        (res?.errors ? Object.values(res.errors).flat().join(' ') : null) ??
        res?.detail ??
        res?.error ??
        'Erro ao criar barbeiro. Verifique os dados e tente novamente.'
      setServerError(message)
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4 max-h-[70vh] overflow-y-auto pr-1">
      <Input label="Nome completo" type="text" placeholder="Carlos Barbeiro" error={errors.name?.message} {...register('name')} />
      <Input label="E-mail" type="email" placeholder="carlos@imperador.com" error={errors.email?.message} {...register('email')} />
      <Input label="Senha" type="password" placeholder="••••••••" error={errors.password?.message} {...register('password')} />
      <Input label="Confirmar senha" type="password" placeholder="••••••••" error={errors.confirmPassword?.message} {...register('confirmPassword')} />
      <div className="flex flex-col gap-1">
        <label className="text-sm font-medium text-brand-white/80">Foto (opcional)</label>
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
      <AvailabilityPicker control={control} register={register} watch={watch} errors={errors} />
      {serverError && <p role="alert" className="text-sm text-red-400">{serverError}</p>}
      <Button type="submit" isLoading={isSubmitting} className="mt-2 w-full">Criar barbeiro</Button>
    </form>
  )
}

function EditBarberForm({ barber, onSuccess }: { barber: AdminBarber; onSuccess: () => void }) {
  const updateBarber = useUpdateBarber()
  const [serverError, setServerError] = useState<string | null>(null)

  const defaultAvailability = WEEKDAYS.map((d) => {
    const existing = barber.availability.find(
      (a) => DAY_OF_WEEK_NUMS[a.dayOfWeek] === d.value
    )
    return {
      dayOfWeek: d.value,
      startTime: existing ? existing.startTime.slice(0, 5) : '09:00',
      endTime: existing ? existing.endTime.slice(0, 5) : '18:00',
      enabled: !!existing,
    }
  })

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<EditBarberFormData>({
    resolver: zodResolver(editBarberSchema),
    defaultValues: {
      name: barber.name,
      email: barber.email,
      password: '',
      confirmPassword: '',
      availability: defaultAvailability,
    },
  })

  async function onSubmit(data: EditBarberFormData) {
    setServerError(null)
    const availability = data.availability
      .filter((a) => a.enabled)
      .map((a) => ({
        dayOfWeek: DAY_OF_WEEK_STRINGS[a.dayOfWeek],
        startTime: `${a.startTime}:00`,
        endTime: `${a.endTime}:00`,
      }))

    try {
      await updateBarber.mutateAsync({
        id: barber.id,
        name: data.name,
        email: data.email,
        password: data.password || undefined,
        availability,
        photo: data.photo,
      })
      onSuccess()
    } catch (err: unknown) {
      const axiosErr = err as {
        response?: { data?: { detail?: string; error?: string; errors?: Record<string, string[]> } }
      }
      const res = axiosErr?.response?.data
      const message =
        (res?.errors ? Object.values(res.errors).flat().join(' ') : null) ??
        res?.detail ??
        res?.error ??
        'Erro ao atualizar barbeiro. Verifique os dados e tente novamente.'
      setServerError(message)
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4 max-h-[70vh] overflow-y-auto pr-1">
      <Input label="Nome completo" type="text" error={errors.name?.message} {...register('name')} />
      <Input label="E-mail" type="email" error={errors.email?.message} {...register('email')} />
      <Input label="Nova senha (deixe em branco para manter)" type="password" placeholder="••••••••" error={errors.password?.message} {...register('password')} />
      <Input label="Confirmar nova senha" type="password" placeholder="••••••••" error={errors.confirmPassword?.message} {...register('confirmPassword')} />
      <div className="flex flex-col gap-1">
        <label className="text-sm font-medium text-brand-white/80">Nova foto (opcional)</label>
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
      <AvailabilityPicker control={control} register={register} watch={watch} errors={errors} />
      {serverError && <p role="alert" className="text-sm text-red-400">{serverError}</p>}
      <Button type="submit" isLoading={isSubmitting} className="mt-2 w-full">Salvar alterações</Button>
    </form>
  )
}

function DeleteBarberConfirm({
  barber,
  onClose,
}: {
  barber: AdminBarber
  onClose: () => void
}) {
  const deleteBarber = useDeleteBarber()
  const [error, setError] = useState<string | null>(null)

  async function handleDelete() {
    setError(null)
    try {
      await deleteBarber.mutateAsync(barber.id)
      onClose()
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string } } }
      setError(axiosErr?.response?.data?.error ?? 'Erro ao excluir barbeiro.')
    }
  }

  return (
    <Modal isOpen onClose={onClose} title="Excluir barbeiro">
      <div className="flex flex-col gap-4">
        <p className="text-brand-white/80">
          Deseja excluir permanentemente o barbeiro <strong>{barber.name}</strong>?
          Esta ação não pode ser desfeita.
        </p>
        {error && <p role="alert" className="text-sm text-red-400">{error}</p>}
        <div className="flex gap-3 justify-end">
          <Button variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button variant="danger" onClick={handleDelete} isLoading={deleteBarber.isPending}>
            Excluir
          </Button>
        </div>
      </div>
    </Modal>
  )
}

export default function BarbersPage() {
  const { data: barbers, isLoading } = useAdminBarbers()
  const deactivate = useDeactivateBarber()
  const activate = useActivateBarber()
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [editTarget, setEditTarget] = useState<AdminBarber | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<AdminBarber | null>(null)

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="font-montserrat text-2xl font-black text-brand-white">Barbeiros</h1>
        <Button onClick={() => setShowCreateModal(true)} className="w-full sm:w-auto">
          Adicionar Barbeiro
        </Button>
      </div>

      <div className="flex flex-col gap-4">
        {barbers?.map((barber) => (
          <article
            key={barber.id}
            className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-4"
          >
            <div className="flex items-start gap-3">
              {barber.photoUrl ? (
                <Image
                  src={barber.photoUrl}
                  alt={barber.name}
                  width={44}
                  height={44}
                  className="h-11 w-11 shrink-0 rounded-full object-cover"
                />
              ) : (
                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-brand-gold/20 text-sm font-bold text-brand-gold">
                  {barber.name.charAt(0).toUpperCase()}
                </div>
              )}
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium text-brand-white">{barber.name}</span>
                  <span
                    className={[
                      'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold',
                      barber.isActive ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400',
                    ].join(' ')}
                  >
                    {barber.isActive ? 'Ativo' : 'Inativo'}
                  </span>
                </div>
                <p className="truncate text-sm text-brand-white/50">{barber.email}</p>
                <p className="text-sm text-brand-gold">
                  {barber.averageRating > 0 ? `${barber.averageRating.toFixed(1)} ★` : 'Sem avaliações'}
                </p>
              </div>
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <Button variant="secondary" size="sm" onClick={() => setEditTarget(barber)}>
                Editar
              </Button>
              {barber.isActive ? (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => deactivate.mutate(barber.id)}
                  isLoading={deactivate.isPending && deactivate.variables === barber.id}
                >
                  Desativar
                </Button>
              ) : (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => activate.mutate(barber.id)}
                  isLoading={activate.isPending && activate.variables === barber.id}
                >
                  Ativar
                </Button>
              )}
              <Button variant="danger" size="sm" onClick={() => setDeleteTarget(barber)}>
                Excluir
              </Button>
            </div>

            {/* <details> em vez de estado: bloqueios e atendimentos são longos e
                empurravam o próximo barbeiro para fora da tela */}
            <details className="group mt-4 border-t border-brand-white/10 pt-3">
              <summary className="cursor-pointer list-none text-sm text-brand-white/60 transition-colors hover:text-brand-gold">
                <span className="inline-block transition-transform group-open:rotate-90">›</span> Bloqueios de agenda
              </summary>
              <div className="mt-3">
                <AdminBlocksSection barberId={barber.id} />
              </div>
            </details>

            <details className="group mt-2 border-t border-brand-white/10 pt-3">
              <summary className="cursor-pointer list-none text-sm text-brand-white/60 transition-colors hover:text-brand-gold">
                <span className="inline-block transition-transform group-open:rotate-90">›</span> Atendimentos
              </summary>
              <div className="mt-3">
                <AdminAppointmentsSection barberId={barber.id} />
              </div>
            </details>
          </article>
        ))}

        {barbers?.length === 0 && (
          <p className="rounded-xl border border-brand-white/10 bg-brand-black-soft px-4 py-8 text-center text-brand-white/40">
            Nenhum barbeiro cadastrado.
          </p>
        )}
      </div>

      <Modal isOpen={showCreateModal} onClose={() => setShowCreateModal(false)} title="Adicionar Barbeiro">
        <CreateBarberForm onSuccess={() => setShowCreateModal(false)} />
      </Modal>

      {editTarget && (
        <Modal isOpen onClose={() => setEditTarget(null)} title={`Editar "${editTarget.name}"`}>
          <EditBarberForm barber={editTarget} onSuccess={() => setEditTarget(null)} />
        </Modal>
      )}

      {deleteTarget && (
        <DeleteBarberConfirm barber={deleteTarget} onClose={() => setDeleteTarget(null)} />
      )}
    </div>
  )
}
