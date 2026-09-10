using ImperadorBarberShop.Infrastructure.Services;

namespace ImperadorBarberShop.UnitTests;

/// <summary>
/// O <see cref="ShopTimeProvider"/> da aplicação parado num instante: fuso de São
/// Paulo, UTC fixo. Os testes de "agora" usam um servidor em UTC com a barbearia 3 horas
/// atrás — o cenário em que comparar horário de parede com UtcNow erra.
/// </summary>
public sealed class FixedShopClock : TimeProvider
{
    /// <summary>2026-09-10 21:58 UTC = 18:58 em São Paulo (quinta-feira).</summary>
    public static readonly DateTimeOffset EveningUtc = new(2026, 9, 10, 21, 58, 0, TimeSpan.Zero);

    private readonly DateTimeOffset _utcNow;

    public FixedShopClock(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override TimeZoneInfo LocalTimeZone => ShopTimeProvider.ShopTimeZone;

    /// <summary>O "agora" da barbearia, no mesmo formato sem fuso de ScheduledAt.</summary>
    public DateTime ShopNow => GetLocalNow().DateTime;
}
