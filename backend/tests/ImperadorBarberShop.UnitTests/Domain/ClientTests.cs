using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.ValueObjects;

namespace ImperadorBarberShop.UnitTests.Domain;

public class ClientTests
{
    // Horário de parede da barbearia: quinta, 10/09/2026 às 18:58
    private static readonly DateTime Now = new(2026, 9, 10, 18, 58, 0);

    private static Client NewClient() =>
        Client.Create("João", BrazilianPhone.Parse("11 9999-0000"), new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

    private static Client VisitedDaysAgo(int days)
    {
        var client = NewClient();
        client.RegisterVisit(Now.Date.AddDays(-days).AddHours(10));
        return client;
    }

    [Fact]
    public void Create_TakesNameAndCanonicalPhoneAndStartsWithoutVisits()
    {
        var client = NewClient();

        client.Name.Should().Be("João");
        client.Phone.Should().Be("+5511999990000");
        client.MatchKey.Should().Be("1199990000");
        client.FirstSeenAt.Should().Be(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        client.VisitCount.Should().Be(0);
        client.LastVisitAt.Should().BeNull();
        client.LastInviteAt.Should().BeNull();
    }

    [Fact]
    public void RegisterVisit_CountsAndMovesTheLastVisitForward()
    {
        var client = NewClient();

        client.RegisterVisit(new DateTime(2026, 8, 1, 10, 0, 0));
        client.RegisterVisit(new DateTime(2026, 8, 20, 15, 0, 0));

        client.VisitCount.Should().Be(2);
        client.LastVisitAt.Should().Be(new DateTime(2026, 8, 20, 15, 0, 0));
    }

    [Fact]
    public void RegisterVisit_CompletingAnOlderAppointmentLater_DoesNotMoveTheLastVisitBack()
    {
        var client = NewClient();
        client.RegisterVisit(new DateTime(2026, 8, 20, 15, 0, 0));

        client.RegisterVisit(new DateTime(2026, 8, 1, 10, 0, 0));

        client.VisitCount.Should().Be(2);
        client.LastVisitAt.Should().Be(new DateTime(2026, 8, 20, 15, 0, 0));
    }

    [Fact]
    public void DaysSinceLastVisit_CountsCalendarDays()
    {
        var client = NewClient();
        client.RegisterVisit(new DateTime(2026, 9, 9, 23, 0, 0));

        // Ontem às 23h é "há 1 dia", mesmo com menos de 24h passadas
        client.DaysSinceLastVisit(new DateTime(2026, 9, 10, 0, 30, 0)).Should().Be(1);
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, true)]
    [InlineData(27, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void IsDueForReinvite_OnlyInsideThe25To30DayWindow(int daysAgo, bool expected)
    {
        VisitedDaysAgo(daysAgo).IsDueForReinvite(Now).Should().Be(expected);
    }

    [Fact]
    public void IsDueForReinvite_NeverCompletedAVisit_IsNotDue()
    {
        NewClient().IsDueForReinvite(Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(20, true)]
    public void IsDueForReinvite_InvitedInTheLast10Days_IsNotDue(int invitedDaysAgo, bool expected)
    {
        var client = VisitedDaysAgo(27);
        client.MarkInvited(Now.AddDays(-invitedDaysAgo));

        client.IsDueForReinvite(Now).Should().Be(expected);
    }

    [Fact]
    public void MarkInvited_RecordsTheInviteTime()
    {
        var client = VisitedDaysAgo(27);

        client.MarkInvited(Now);

        client.LastInviteAt.Should().Be(Now);
    }

    [Fact]
    public void MarkInvited_WithinTheCooldown_Throws()
    {
        var client = VisitedDaysAgo(27);
        client.MarkInvited(Now.AddDays(-3));

        var act = () => client.MarkInvited(Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*últimos 10 dias*");
        client.LastInviteAt.Should().Be(Now.AddDays(-3));
    }
}
