'use client'

import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { Input } from '@/components/ui/Input'
import { Button } from '@/components/ui/Button'
import { authApi } from '@/lib/api/auth.api'
import { useAuth } from '@/hooks/useAuth'
import type { BarberAvailability } from '@/types/api.types'

const WEEKDAYS = [
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

const registerSchema = z
  .object({
    name: z.string().min(2, 'Nome deve ter pelo menos 2 caracteres'),
    email: z.string().email('E-mail inválido'),
    password: z.string().min(8, 'Senha deve ter pelo menos 8 caracteres'),
    confirmPassword: z.string(),
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

type RegisterFormData = z.infer<typeof registerSchema>

export function BarberRegisterForm() {
  const { login } = useAuth()
  const router = useRouter()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    control,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      availability: WEEKDAYS.map((d) => ({
        dayOfWeek: d.value,
        startTime: '09:00',
        endTime: '18:00',
        enabled: d.value >= 1 && d.value <= 5, // Mon-Fri enabled by default
      })),
    },
  })

  const { fields } = useFieldArray({ control, name: 'availability' })
  const availabilityValues = watch('availability')

  async function onSubmit(data: RegisterFormData) {
    setServerError(null)
    const availability: BarberAvailability[] = data.availability
      .filter((a) => a.enabled)
      .map((a) => ({
        dayOfWeek: a.dayOfWeek,
        startTime: `${a.startTime}:00`,
        endTime: `${a.endTime}:00`,
      }))

    try {
      const res = await authApi.registerBarber({
        name: data.name,
        email: data.email,
        password: data.password,
        availability,
      })
      login(res.data)
      router.push('/barber/dashboard')
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { detail?: string; errors?: Record<string, string[]> } } }
      const data = axiosErr?.response?.data
      const message =
        (data?.errors ? Object.values(data.errors).flat().join(' ') : null) ??
        data?.detail ??
        'Erro ao criar conta. Verifique os dados e tente novamente.'
      setServerError(message)
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
      <Input
        label="Nome completo"
        type="text"
        autoComplete="name"
        placeholder="Carlos Barbeiro"
        error={errors.name?.message}
        {...register('name')}
      />
      <Input
        label="E-mail"
        type="email"
        autoComplete="email"
        placeholder="seu@email.com"
        error={errors.email?.message}
        {...register('email')}
      />
      <Input
        label="Senha"
        type="password"
        autoComplete="new-password"
        placeholder="••••••••"
        error={errors.password?.message}
        {...register('password')}
      />
      <Input
        label="Confirmar senha"
        type="password"
        autoComplete="new-password"
        placeholder="••••••••"
        error={errors.confirmPassword?.message}
        {...register('confirmPassword')}
      />

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
                <div className={['flex items-center gap-2 transition-opacity', isEnabled ? 'opacity-100' : 'opacity-30'].join(' ')}>
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
        Criar conta de barbeiro
      </Button>
    </form>
  )
}
