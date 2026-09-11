/**
 * Espelho do `BrazilianPhone` do backend: aceita o celular do jeito que a pessoa
 * digitou ("+55 (11) 99999-0000", "011 99999-0000", "11 9999-0000") e devolve a forma
 * canônica `+55DDD9XXXXXXXX`. O botão de confirmar habilita exatamente para o que a
 * API aceita — divergir aqui é recusar na tela um número que o servidor aceitaria.
 */
export function normalizeBrPhone(raw: string): string | null {
  const trimmed = raw.trim()
  let digits = trimmed.replace(/\D/g, '')

  // "+" anuncia um código de país: qualquer um que não seja 55 é estrangeiro
  if (trimmed.startsWith('+') && !digits.startsWith('55')) return null

  // Zeros à esquerda são prefixo de discagem; nenhum DDD começa com 0
  digits = digits.replace(/^0+/, '')

  // Número nacional tem no máximo 11 dígitos: sobrando, o "55" do início é o país
  if (digits.length > 11 && digits.startsWith('55')) digits = digits.slice(2).replace(/^0+/, '')

  if (digits.length !== 10 && digits.length !== 11) return null

  const ddd = digits.slice(0, 2)
  if (ddd[1] === '0') return null

  let subscriber = digits.slice(2)
  if (subscriber.length === 8) {
    // Sem o nono dígito, só celular antigo (6–9) ganha o 9; um fixo viraria outro número
    if (subscriber[0] < '6') return null
    subscriber = `9${subscriber}`
  }

  return `+55${ddd}${subscriber}`
}

export function isValidBrPhone(raw: string): boolean {
  return normalizeBrPhone(raw) !== null
}

/** "+5511999990000" → "(11) 99999-0000". Devolve o texto como veio se não for canônico. */
export function formatBrPhone(canonical: string): string {
  const match = /^\+55(\d{2})(\d{5})(\d{4})$/.exec(canonical)
  return match ? `(${match[1]}) ${match[2]}-${match[3]}` : canonical
}
