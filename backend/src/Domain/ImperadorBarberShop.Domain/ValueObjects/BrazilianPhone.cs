using System.Diagnostics.CodeAnalysis;

namespace ImperadorBarberShop.Domain.ValueObjects;

/// <summary>
/// Um celular brasileiro digitado de qualquer jeito — "+55 (11) 99999-0000", "011 99999-0000",
/// "11 9999-0000" — reduzido às duas formas que o sistema usa. É o único lugar com essas regras:
/// a validação do agendamento, a identificação do cliente e o backfill da migração passam por aqui.
/// </summary>
public sealed record BrazilianPhone
{
    public const string InvalidMessage =
        "Número de WhatsApp inválido. Informe o DDD e o celular, como 11 99999-0000.";

    /// <summary>Forma de exibição e envio: <c>+55DDD9XXXXXXXX</c>.</summary>
    public string Canonical { get; }

    /// <summary>
    /// Identidade do cliente: DDD + os 8 últimos dígitos. Ignora o nono dígito do celular,
    /// que muita gente ainda omite, para que "11 99999-0000" e "11 9999-0000" sejam a mesma pessoa.
    /// </summary>
    public string MatchKey { get; }

    private BrazilianPhone(string canonical, string matchKey)
    {
        Canonical = canonical;
        MatchKey = matchKey;
    }

    public static BrazilianPhone Parse(string? input)
        => TryParse(input, out var phone) ? phone : throw new FormatException(InvalidMessage);

    public static bool TryParse(string? input, [NotNullWhen(true)] out BrazilianPhone? phone)
    {
        phone = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());

        // "+" anuncia um código de país: qualquer um que não seja 55 é número estrangeiro
        if (input.TrimStart().StartsWith('+') && !digits.StartsWith("55")) return false;

        // Zeros à esquerda são prefixo de discagem ("0 11", "00 55"); nenhum DDD começa com 0
        digits = digits.TrimStart('0');

        // Número nacional tem no máximo 11 dígitos: sobrando, o "55" do início é o país
        if (digits.Length > 11 && digits.StartsWith("55"))
            digits = digits[2..].TrimStart('0');

        if (digits.Length is not (10 or 11)) return false;

        var ddd = digits[..2];
        if (ddd[1] == '0') return false;

        var subscriber = digits[2..];
        if (subscriber.Length == 8)
        {
            // Sem o nono dígito, só um celular antigo (6–9) ganha o 9 da frente; um fixo
            // (2–5) viraria o número de outra pessoa
            if (subscriber[0] < '6') return false;
            subscriber = "9" + subscriber;
        }

        phone = new BrazilianPhone($"+55{ddd}{subscriber}", ddd + subscriber[^8..]);
        return true;
    }
}
