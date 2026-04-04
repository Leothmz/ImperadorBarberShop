'use client'

import { useSearchParams } from 'next/navigation'

// Extracted into its own Client Component so the parent page can remain a
// Server Component and wrap this with Suspense (required by Next.js App Router
// whenever useSearchParams() is used during static generation).
export function LoginPageContent() {
  const searchParams = useSearchParams()
  const justRegistered = searchParams.get('registered') === '1'

  if (!justRegistered) return null

  return (
    <div
      role="status"
      className="mb-4 rounded-lg border border-green-500/30 bg-green-500/10 px-4 py-3 text-sm text-green-400"
    >
      Conta criada com sucesso! Faça login para continuar.
    </div>
  )
}
