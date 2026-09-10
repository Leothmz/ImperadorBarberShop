namespace ImperadorBarberShop.Infrastructure.Services;

/// <summary>
/// Relógio da barbearia: o instante continua vindo do UTC do sistema, mas o fuso
/// local é o de São Paulo, qualquer que seja o fuso do servidor.
/// </summary>
/// <remarks>
/// Horários de agendamento, disponibilidade e bloqueios são guardados como horário
/// de parede da barbearia, sem fuso ("2026-09-11T19:30:00"). Todo "agora" comparado
/// com eles tem de ser <c>GetLocalNow().DateTime</c> — nunca <c>DateTime.UtcNow</c>,
/// que num host em UTC adianta o relógio em 3 horas.
/// </remarks>
public sealed class ShopTimeProvider : TimeProvider
{
    public const string ShopTimeZoneId = "America/Sao_Paulo";

    public static readonly TimeZoneInfo ShopTimeZone = ResolveShopTimeZone();

    public override TimeZoneInfo LocalTimeZone => ShopTimeZone;

    private static TimeZoneInfo ResolveShopTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ShopTimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Host sem tzdata (imagem mínima): o Brasil não tem horário de verão desde
            // 2019, então UTC−3 fixo dá o mesmo resultado que a base de fusos.
            return TimeZoneInfo.CreateCustomTimeZone(
                ShopTimeZoneId, TimeSpan.FromHours(-3), "Horário de Brasília", "Horário de Brasília");
        }
    }
}
