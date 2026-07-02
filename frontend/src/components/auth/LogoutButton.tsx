'use client'

import { useAuthContext } from '@/providers/AuthProvider'
import { Button } from '@/components/ui/Button'
import { useRouter } from 'next/navigation'

export function LogoutButton() {
  const { logout } = useAuthContext()
  const router = useRouter()

  function handleLogout() {
    logout()
    router.push('/login')
  }

  return (
    <Button variant="secondary" size="sm" onClick={handleLogout} className="w-full">
      Sair
    </Button>
  )
}
