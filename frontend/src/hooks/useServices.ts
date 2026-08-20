import { useQuery } from '@tanstack/react-query'
import { servicesApi } from '@/lib/api/services.api'

/**
 * Catálogo público. É a mesma fonte da tabela na home e do passo de serviços do
 * agendamento — nada aqui é fixo no código, então o que o admin salvar aparece
 * sem deploy.
 *
 * A política de atualização é mais curta que o padrão do app porque preço e
 * duração mudam pelo painel enquanto a página de alguém está aberta:
 * revalida ao voltar para a aba, ao reconectar, e a cada minuto. O intervalo
 * não roda em aba de fundo (padrão do TanStack), então uma aba esquecida não
 * fica batendo na API.
 */
export function useServices() {
  return useQuery({
    queryKey: ['services'],
    queryFn: () => servicesApi.getAll().then((r) => r.data),
    staleTime: 30 * 1000,
    refetchOnWindowFocus: true,
    refetchOnReconnect: true,
    refetchInterval: 60 * 1000,
  })
}
