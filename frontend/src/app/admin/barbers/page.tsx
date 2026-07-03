'use client'

import { useState } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import Image from 'next/image'
import {
  useAdminBarbers,
  useCreateBarber,
  useDeactivateBarber,
  useActivateBarber,
} from '@/hooks/useAdminBarbers'
import AdminBlocksSection from './AdminBlocksSection'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Spinner } from '@/components/ui/Spinner'
import type { DayOfWeekString } from '@/types/api.types'

const DAY_OF_WEEK_STRINGS: Record<number, DayOfWeekString> = {
  0: 'Sunday',
  1: 'Monday',
  2: 'Tuesday',
  3: 'Wednesday',
  4: 'Thursday',
  5: 'Friday',
  6: 'Saturday',
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

type CreateBarberFormData = z.infer<typeof createBarberSchema>

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

  const { fields } = useFieldArray({ control, name: 'availability' })
  const availabilityValues = watch('availability')

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
        response?: { data?: { detail?: string; errors?: Record<string, string[]> } }
      }
      const res = axiosErr?.response?.data
      const message =
        (res?.errors ? Object.values(res.errors).flat().join(' ') : null) ??
        res?.detail ??
        'Erro ao criar barbeiro. Verifique os dados e tente novamente.'
      setServerError(message)
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4 max-h-[70vh] overflow-y-auto pr-1">
      <Input
        label="Nome completo"
        type="text"
        placeholder="Carlos Barbeiro"
        error={errors.name?.message}
        {...register('name')}
      />
      <Input
        label="E-mail"
        type="email"
        placeholder="carlos@imperador.com"
        error={errors.email?.message}
        {...register('email')}
      />
      <Input
        label="Senha"
        type="password"
        placeholder="••••••••"
        error={errors.password?.message}
        {...register('password')}
      />
      <Input
        label="Confirmar senha"
        type="password"
        placeholder="••••••••"
        error={errors.confirmPassword?.message}
        {...register('confirmPassword')}
      />

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

      {/* Availability */}
      <fieldset className="rounded-lg border border-brand-white/10 p-4">
        <legend className="px-1 text-sm font-medium text-brand-white/80">
          Disponibilidade semanal
        </legend>
        <div className="flex flex-col gap-3 mt-2">
          {fields.map((field, index) => {
            const day = WEEKDAYS[index]
            const isEnabled = availabilityValues[index]?.enabled

            return (
              <div key={field.id} className="flex items-center gap-3 flex-wrap">
                <Controller
                  control={control}
                  name={`availability.${index}.enabled`}
                  render={({ field: { value, onChange } }) => (
                    <label className="flex items-center gap-2 w-24 cursor-pointer">
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
                    'flex items-center gap-2 transition-opacity',
                    isEnabled ? 'opacity-100' : 'opacity-30',
                  ].join(' ')}
                >
                  <label className="text-xs text-brand-white/50">Início</label>
                  <input
                    type="time"
                    disabled={!isEnabled}
                    className="rounded border border-brand-white/20 bg-brand-black-soft px-2 py-1 text-sm text-brand-white focus:border-brand-gold focus:outline-none"
                    {...register(`availability.${index}.startTime`)}
                  />
                  <label className="text-xs text-brand-white/50">Fim</label>
                  <input
                    type="time"
                    disabled={!isEnabled}
                    className="rounded border border-brand-white/20 bg-brand-black-soft px-2 py-1 text-sm text-brand-white focus:border-brand-gold focus:outline-none"
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

      {serverError && (
        <p role="alert" className="text-sm text-red-400">
          {serverError}
        </p>
      )}

      <Button type="submit" isLoading={isSubmitting} className="mt-2 w-full">
        Criar barbeiro
      </Button>
    </form>
  )
}

export default function BarbersPage() {
  const { data: barbers, isLoading } = useAdminBarbers()
  const deactivate = useDeactivateBarber()
  const activate = useActivateBarber()
  const [showModal, setShowModal] = useState(false)

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
          Barbeiros
        </h1>
        <Button onClick={() => setShowModal(true)}>Adicionar Barbeiro</Button>
      </div>

      <div className="overflow-x-auto rounded-xl border border-brand-white/10">
        <table className="w-full text-sm text-brand-white/80">
          <thead>
            <tr className="border-b border-brand-white/10 bg-brand-black-soft text-left text-brand-white/40">
              <th className="px-4 py-3">Barbeiro</th>
              <th className="px-4 py-3">E-mail</th>
              <th className="px-4 py-3">Avaliação</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Ações</th>
            </tr>
          </thead>
          <tbody>
            {barbers?.map((barber) => (
              <>
              <tr
                key={barber.id}
                className="border-b border-brand-white/5 hover:bg-brand-white/5 transition-colors"
              >
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    {barber.photoUrl ? (
                      <Image
                        src={barber.photoUrl}
                        alt={barber.name}
                        width={36}
                        height={36}
                        className="rounded-full object-cover"
                      />
                    ) : (
                      <div className="h-9 w-9 rounded-full bg-brand-gold/20 flex items-center justify-center text-brand-gold font-bold text-sm">
                        {barber.name.charAt(0).toUpperCase()}
                      </div>
                    )}
                    <span className="font-medium text-brand-white">{barber.name}</span>
                  </div>
                </td>
                <td className="px-4 py-3 text-brand-white/60">{barber.email}</td>
                <td className="px-4 py-3">
                  <span className="text-brand-gold">
                    {barber.averageRating > 0
                      ? `${barber.averageRating.toFixed(1)} ★`
                      : '—'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span
                    className={[
                      'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold',
                      barber.isActive
                        ? 'bg-green-500/20 text-green-400'
                        : 'bg-red-500/20 text-red-400',
                    ].join(' ')}
                  >
                    {barber.isActive ? 'Ativo' : 'Inativo'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  {barber.isActive ? (
                    <Button
                      variant="danger"
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
                </td>
              </tr>
              <tr key={`${barber.id}-blocks`} className="border-b border-brand-white/5">
                <td colSpan={5} className="px-4 pb-4">
                  <AdminBlocksSection barberId={barber.id} />
                </td>
              </tr>
              </>
            ))}
            {barbers?.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-brand-white/40">
                  Nenhum barbeiro cadastrado.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title="Adicionar Barbeiro"
      >
        <CreateBarberForm onSuccess={() => setShowModal(false)} />
      </Modal>
    </div>
  )
}
