'use client'

import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  useWhatsAppStatus,
  useWhatsAppQr,
  useDisconnectWhatsApp,
  useNotificationSettings,
  useUpdateNotificationSettings,
} from '@/hooks/useWhatsApp'

type Tab = 'connection' | 'notifications'

export default function WhatsAppPage() {
  const [tab, setTab] = useState<Tab>('connection')

  return (
    <div>
      <h1 className="font-montserrat text-2xl font-bold text-brand-gold mb-6">WhatsApp</h1>

      <div className="flex gap-2 mb-6 border-b border-brand-white/10">
        {(['connection', 'notifications'] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              tab === t
                ? 'border-b-2 border-brand-gold text-brand-gold'
                : 'text-brand-white/60 hover:text-brand-white'
            }`}
          >
            {t === 'connection' ? 'Conexão' : 'Notificações'}
          </button>
        ))}
      </div>

      {tab === 'connection' ? <ConnectionTab /> : <NotificationsTab />}
    </div>
  )
}

function ConnectionTab() {
  const { data: status, isLoading } = useWhatsAppStatus()
  const isQrRequired = status?.status === 'qr_required'
  const { data: qr } = useWhatsAppQr(isQrRequired)
  const disconnect = useDisconnectWhatsApp()

  if (isLoading) return <p className="text-brand-white/50">Verificando conexão...</p>

  const statusLabel = {
    connected: { text: 'Conectado', color: 'text-green-400' },
    qr_required: { text: 'Aguardando QR Code', color: 'text-yellow-400' },
    disconnected: { text: 'Desconectado', color: 'text-red-400' },
  }[status?.status ?? 'disconnected']

  return (
    <div className="flex flex-col gap-6 max-w-md">
      <div className="bg-brand-black-soft rounded-lg p-4 flex items-center gap-3">
        <span className={`font-semibold ${statusLabel.color}`}>{statusLabel.text}</span>
        {status?.phoneNumber && (
          <span className="text-brand-white/60 text-sm">{status.phoneNumber}</span>
        )}
      </div>

      {isQrRequired && qr && (
        <div className="flex flex-col items-center gap-3">
          <p className="text-brand-white/70 text-sm">
            Abra o WhatsApp no celular → Dispositivos vinculados → Vincular um dispositivo
          </p>
          <img
            src={qr.qrCode}
            alt="QR Code WhatsApp"
            className="w-64 h-64 rounded-lg"
          />
        </div>
      )}

      {status?.status === 'disconnected' && (
        <p className="text-brand-white/60 text-sm">Escaneie o QR code para conectar</p>
      )}

      {status?.status === 'connected' && (
        <button
          onClick={() => disconnect.mutate()}
          disabled={disconnect.isPending}
          className="px-4 py-2 rounded-lg bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors text-sm w-fit disabled:opacity-50"
        >
          {disconnect.isPending ? 'Desconectando...' : 'Desconectar'}
        </button>
      )}
    </div>
  )
}

const notificationSchema = z.object({
  channels: z.enum(['email', 'whatsapp', 'email,whatsapp']),
  reminderMinutesBefore: z.number().int().min(5).max(1440),
  notificationPhone: z.string().optional(),
})

type NotificationFormValues = z.infer<typeof notificationSchema>

function NotificationsTab() {
  const { data: settings } = useNotificationSettings()
  const updateSettings = useUpdateNotificationSettings()
  const [saved, setSaved] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<NotificationFormValues>({
    resolver: zodResolver(notificationSchema),
    defaultValues: {
      channels: 'email',
      reminderMinutesBefore: 60,
      notificationPhone: '',
    },
  })

  useEffect(() => {
    if (settings) {
      reset({
        channels: (settings.channels ?? []).join(',') as 'email' | 'whatsapp' | 'email,whatsapp',
        reminderMinutesBefore: settings.reminderMinutesBefore,
        notificationPhone: settings.notificationPhone ?? '',
      })
    }
  }, [settings, reset])

  const onSubmit = handleSubmit(async (data) => {
    await updateSettings.mutateAsync({
      channels: data.channels.split(','),
      reminderMinutesBefore: data.reminderMinutesBefore,
      notificationPhone: data.notificationPhone || null,
    })
    setSaved(true)
    setTimeout(() => setSaved(false), 3000)
  })

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-6 max-w-md">
      <div className="bg-brand-black-soft rounded-lg p-4 flex flex-col gap-3">
        <p className="text-brand-white/70 text-sm font-medium">Canais de notificação</p>
        <div className="space-y-2">
          {[
            { value: 'email', label: 'Apenas Email' },
            { value: 'whatsapp', label: 'Apenas WhatsApp' },
            { value: 'email,whatsapp', label: 'Email e WhatsApp' },
          ].map((opt) => (
            <label key={opt.value} className="flex items-center gap-2 cursor-pointer">
              <input type="radio" value={opt.value} {...register('channels')} />
              <span className="text-brand-white">{opt.label}</span>
            </label>
          ))}
        </div>
        {errors.channels && (
          <p className="text-red-400 text-xs">{errors.channels.message}</p>
        )}
      </div>

      <div className="bg-brand-black-soft rounded-lg p-4 flex flex-col gap-3">
        <label className="flex flex-col gap-1">
          <span className="text-brand-white/70 text-sm">Lembrete (minutos antes)</span>
          <input
            type="number"
            min={5}
            max={1440}
            {...register('reminderMinutesBefore', { valueAsNumber: true })}
            className="bg-brand-black border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 w-32 focus:outline-none focus:border-brand-gold"
          />
          {errors.reminderMinutesBefore && (
            <p className="text-red-400 text-xs">{errors.reminderMinutesBefore.message}</p>
          )}
        </label>

        <label className="flex flex-col gap-1">
          <span className="text-brand-white/70 text-sm">
            Telefone de notificação dos barbeiros (opcional)
          </span>
          <input
            type="text"
            {...register('notificationPhone')}
            placeholder="+5511999990000"
            className="bg-brand-black border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 focus:outline-none focus:border-brand-gold"
          />
          {errors.notificationPhone && (
            <p className="text-red-400 text-xs">{errors.notificationPhone.message}</p>
          )}
        </label>
      </div>

      <div className="flex items-center gap-3">
        <button
          type="submit"
          disabled={updateSettings.isPending}
          className="px-6 py-2 bg-brand-gold text-brand-black font-semibold rounded-lg hover:bg-brand-gold-light transition-colors disabled:opacity-50"
        >
          {updateSettings.isPending ? 'Salvando...' : 'Salvar'}
        </button>
        {saved && <span className="text-green-400 text-sm">Configurações salvas!</span>}
      </div>
    </form>
  )
}
