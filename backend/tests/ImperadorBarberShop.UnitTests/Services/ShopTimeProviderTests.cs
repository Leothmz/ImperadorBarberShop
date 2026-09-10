using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Services;

namespace ImperadorBarberShop.UnitTests.Services;

public class ShopTimeProviderTests
{
    [Fact]
    public void LocalTimeZone_IsSaoPaulo_RegardlessOfServerZone()
    {
        var clock = new ShopTimeProvider();

        clock.LocalTimeZone.GetUtcOffset(new DateTime(2026, 9, 10, 21, 58, 0, DateTimeKind.Utc))
            .Should().Be(TimeSpan.FromHours(-3));
    }

    [Fact]
    public void GetLocalNow_IsWallClockThreeHoursBehindUtc()
    {
        var clock = new ShopTimeProvider();

        var utc = clock.GetUtcNow();
        var local = clock.GetLocalNow();

        local.Offset.Should().Be(TimeSpan.FromHours(-3));
        local.UtcDateTime.Should().BeCloseTo(utc.UtcDateTime, TimeSpan.FromSeconds(5));
    }
}
