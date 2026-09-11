import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'

const reinviteCandidatesKey = ['admin', 'clients', 'reinvite-candidates'] as const

export function useReinviteCandidates() {
  return useQuery({
    queryKey: reinviteCandidatesKey,
    queryFn: adminApi.getReinviteCandidates,
  })
}

export function useReinviteClient() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (clientId: string) => adminApi.reinviteClient(clientId),
    // Convidado sai da lista por 10 dias; uma recusa (convite recente feito em outra aba)
    // também quer dizer que a lista envelheceu. Recarrega em vez de adivinhar.
    onSettled: () => queryClient.invalidateQueries({ queryKey: reinviteCandidatesKey }),
  })
}
