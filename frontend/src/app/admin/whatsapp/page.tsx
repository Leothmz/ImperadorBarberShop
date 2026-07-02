'use client'

import { useState } from 'react'
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
      <h1 className="font-montserrat text-2xl font-bold text-[#C9A84C] mb-6">WhatsApp</h1>

      <div className="flex gap-2 mb-6 border-b border-[#F5F5F5]/10">
        {(['connection', 'notifications'] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              tab === t
                ? 'border-b-2 border-[#C9A84C] text-[#C9A84C]'
                : 'text-[#F5F5F5]/60 hover:text-[#F5F5F5]'
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
  const { data: qr } = useWhatsAppQr()
  const disconnect = useDisconnectWhatsApp()

  if (isLoading) return <p className="text-[#F5F5F5]/50">Verificando conexão...</p>

  const statusLabel = {
    connected: { text: 'Conectado', color: 'text-green-400' },
    qr_required: { text: 'Aguardando QR Code', color: 'text-yellow-400' },
    disconnected: { text: 'Desconectado', color: 'text-red-400' },
  }[status?.status ?? 'disconnected']

  return (
    <div className="flex flex-col gap-6 max-w-md">
      <div className="bg-[#1A1A1A] rounded-lg p-4 flex items-center gap-3">
        <span className={`font-semibold ${statusLabel.color}`}>{statusLabel.text}</span>
        {status?.phoneNumber && (
          <span className="text-[#F5F5F5]/60 text-sm">{status.phoneNumber}</span>
        )}
      </div>

      {status?.status === 'qr_required' && qr && (
        <div className="flex flex-col items-center gap-3">
          <p className="text-[#F5F5F5]/70 text-sm">
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
        <p className="text-[#F5F5F5]/60 text-sm">Escaneie o QR code para conectar</p>
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

function NotificationsTab() {
  const { data: settings } = useNotificationSettings()
  const update = useUpdateNotificationSettings()
  const [channels, setChannels] = useState<string[]>(() => settings?.channels ?? ['email', 'whatsapp'])
  const [minutes, setMinutes] = useState<number>(() => settings?.reminderMinutesBefore ?? 60)
  const [phone, setPhone] = useState<string>(() => settings?.notificationPhone ?? '')
  const [saved, setSaved] = useState(false)

  const toggleChannel = (ch: string) =>
    setChannels((prev) =>
      prev.includes(ch) ? prev.filter((c) => c !== ch) : [...prev, ch]
    )

  const handleSave = () => {
    update.mutate(
      { channels, reminderMinutesBefore: minutes, notificationPhone: phone || null },
      {
        onSuccess: () => {
          setSaved(true)
          setTimeout(() => setSaved(false), 3000)
        },
      }
    )
  }

  return (
    <div className="flex flex-col gap-6 max-w-md">
      <div className="bg-[#1A1A1A] rounded-lg p-4 flex flex-col gap-3">
        <p className="text-[#F5F5F5]/70 text-sm font-medium">Canais de notificação</p>
        {(['email', 'whatsapp'] as const).map((ch) => (
          <label key={ch} className="flex items-center gap-3 cursor-pointer">
            <input
              type="checkbox"
              checked={channels.includes(ch)}
              onChange={() => toggleChannel(ch)}
              className="w-4 h-4 accent-[#C9A84C]"
            />
            <span className="text-[#F5F5F5] capitalize">
              {ch === 'email' ? 'E-mail' : 'WhatsApp'}
            </span>
          </label>
        ))}
      </div>

      <div className="bg-[#1A1A1A] rounded-lg p-4 flex flex-col gap-3">
        <label className="flex flex-col gap-1">
          <span className="text-[#F5F5F5]/70 text-sm">Lembrete (minutos antes)</span>
          <input
            type="number"
            min={5}
            max={1440}
            value={minutes}
            onChange={(e) => setMinutes(Number(e.target.value))}
            className="bg-[#0D0D0D] border border-[#F5F5F5]/20 text-[#F5F5F5] rounded-lg px-3 py-2 w-32 focus:outline-none focus:border-[#C9A84C]"
          />
        </label>

        <label className="flex flex-col gap-1">
          <span className="text-[#F5F5F5]/70 text-sm">
            Telefone de notificação dos barbeiros (opcional)
          </span>
          <input
            type="text"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="+5511999990000"
            className="bg-[#0D0D0D] border border-[#F5F5F5]/20 text-[#F5F5F5] rounded-lg px-3 py-2 focus:outline-none focus:border-[#C9A84C]"
          />
        </label>
      </div>

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={update.isPending}
          className="px-6 py-2 bg-[#C9A84C] text-[#0D0D0D] font-semibold rounded-lg hover:bg-[#E8C96A] transition-colors disabled:opacity-50"
        >
          {update.isPending ? 'Salvando...' : 'Salvar'}
        </button>
        {saved && <span className="text-green-400 text-sm">Configurações salvas!</span>}
      </div>
    </div>
  )
}
