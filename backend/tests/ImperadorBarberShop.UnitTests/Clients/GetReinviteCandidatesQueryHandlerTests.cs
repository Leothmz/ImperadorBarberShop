using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Clients;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Clients;

public class GetReinviteCandidatesQueryHandlerTests
{
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IAppointmentRepository _appointments = Substitute.For<IAppointmentRepository>();
    // Servidor em UTC às 21:58; na barbearia são 18:58 de 10/09/2026
    private readonly FixedShopClock _clock = new(FixedShopClock.EveningUtc);
    private readonly GetReinviteCandidatesQueryHandler _handler;

    public GetReinviteCandidatesQueryHandlerTests()
    {
        _handler = new GetReinviteCandidatesQueryHandler(_clients, _appointments, _clock);
        _appointments.GetClientIdsWithUpcomingAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
    }

    private Client Visited(string name, string phone, int daysAgo, int visits = 1)
    {
        var client = Client.Create(name, BrazilianPhone.Parse(phone), DateTime.UtcNow.AddDays(-90));
        for (var i = 0; i < visits; i++)
            client.RegisterVisit(_clock.ShopNow.Date.AddDays(-daysAgo).AddHours(10));
        return client;
    }

    private void RepositoryHas(params Client[] clients)
        => _clients.GetLastVisitedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(clients.ToList());

    [Fact]
    public async Task Handle_ClientInTheWindow_IsReturnedWithItsRecurrenceData()
    {
        var joao = Visited("João", "11 99999-0000", daysAgo: 27, visits: 4);
        RepositoryHas(joao);

        var result = await _handler.Handle(new GetReinviteCandidatesQuery(), CancellationToken.None);

        var row = result.Should().ContainSingle().Subject;
        row.ClientId.Should().Be(joao.Id);
        row.Name.Should().Be("João");
        row.Phone.Should().Be("+5511999990000");
        row.LastVisitAt.Should().Be(new DateTime(2026, 8, 14, 10, 0, 0));
        row.DaysSinceLastVisit.Should().Be(27);
        row.VisitCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_AsksTheDatabaseOnlyForVisitsSinceTheStartOfDay30()
    {
        RepositoryHas();

        await _handler.Handle(new GetReinviteCandidatesQuery(), CancellationToken.None);

        // Janela medida no relógio da barbearia (10/09), não no UTC do servidor
        await _clients.Received(1).GetLastVisitedSinceAsync(new DateTime(2026, 8, 11), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClientsOutsideThe25To30DayWindow_AreExcluded()
    {
        RepositoryHas(
            Visited("Cedo", "11 91111-0000", daysAgo: 24),
            Visited("Borda25", "11 92222-0000", daysAgo: 25),
            Visited("Borda30", "11 93333-0000", daysAgo: 30),
            Visited("Tarde", "11 94444-0000", daysAgo: 31));

        var result = await _handler.Handle(new GetReinviteCandidatesQuery(), CancellationToken.None);

        result.Select(r => r.Name).Should().BeEquivalentTo("Borda25", "Borda30");
    }

    [Fact]
    public async Task Handle_ClientInvitedInTheLast10Days_IsExcluded()
    {
        var invited = Visited("Convidado", "11 91111-0000", daysAgo: 28);
        invited.MarkInvited(_clock.ShopNow.AddDays(-9));
        var invitedLongAgo = Visited("Convidado Antes", "11 92222-0000", daysAgo: 28);
        invitedLongAgo.MarkInvited(_clock.ShopNow.AddDays(-12));
        RepositoryHas(invited, invitedLongAgo);

        var result = await _handler.Handle(new GetReinviteCandidatesQuery(), CancellationToken.None);

        result.Select(r => r.Name).Should().BeEquivalentTo("Convidado Antes");
    }

    [Fact]
    public async Task Handle_ClientWithAnUpcomingAcceptedAppointment_IsExcluded()
    {
        var booked = Visited("Já Marcou", "11 91111-0000", daysAgo: 26);
        var free = Visited("Sumido", "11 92222-0000", daysAgo: 26);
        RepositoryHas(booked, free);
        _appointments.GetClientIdsWithUpcomingAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(booked.Id) && ids.Contains(free.Id)),
                _clock.ShopNow, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { booked.Id });

        var result = await _handler.Handle(new GetReinviteCandidatesQuery(), CancellationToken.None);

        result.Select(r => r.Name).Should().BeEquivalentTo("Sumido");
    }

    [Fact]
    public async Task Handle_MostOverdueClientsComeFirst()
    {
        RepositoryHas(
            Visited("Bruno", "11 91111-0000", daysAgo: 25),
            Visited("Ana", "11 92222-0000", daysAgo: 29),
            Visited("Carla", "11 93333-0000", daysAgo: 29));

        var result = await _handler.Handle(new GetReinviteCandidatesQuery(), CancellationToken.None);

        result.Select(r => r.Name).Should().ContainInOrder("Ana", "Carla", "Bruno");
    }
}
