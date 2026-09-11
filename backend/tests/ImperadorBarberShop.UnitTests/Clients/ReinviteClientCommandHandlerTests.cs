using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Clients;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Clients;

public class ReinviteClientCommandHandlerTests
{
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IAppSettingsRepository _settings = Substitute.For<IAppSettingsRepository>();
    private readonly INotificationQueue _queue = Substitute.For<INotificationQueue>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    // Servidor em UTC às 21:58; na barbearia são 18:58
    private readonly FixedShopClock _clock = new(FixedShopClock.EveningUtc);
    private readonly ReinviteClientCommandHandler _handler;

    public ReinviteClientCommandHandlerTests()
    {
        _handler = new ReinviteClientCommandHandler(_clients, _settings, _queue, _unitOfWork, _clock);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private Client StoredClient()
    {
        var client = Client.Create("João", BrazilianPhone.Parse("+5511999990000"), DateTime.UtcNow.AddDays(-90));
        client.RegisterVisit(_clock.ShopNow.AddDays(-27));
        _clients.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        return client;
    }

    private void SetChannels(string channels)
        => _settings.GetAsync("notifications:channels", Arg.Any<CancellationToken>()).Returns(channels);

    [Fact]
    public async Task Handle_RecordsLastInviteAtInShopTimeAndSaves()
    {
        SetChannels("email,whatsapp");
        var client = StoredClient();

        await _handler.Handle(new ReinviteClientCommand(client.Id), CancellationToken.None);

        // Horário de parede (18:58), não o UTC do servidor (21:58)
        client.LastInviteAt.Should().Be(_clock.ShopNow);
        await _clients.Received(1).UpdateAsync(client, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QueuesTheWhatsAppInviteInsteadOfAwaitingIt()
    {
        SetChannels("whatsapp");
        var client = StoredClient();
        Func<INotificationService, CancellationToken, Task>? job = null;
        _queue.Enqueue(Arg.Do<Func<INotificationService, CancellationToken, Task>>(j => job = j));

        await _handler.Handle(new ReinviteClientCommand(client.Id), CancellationToken.None);

        job.Should().NotBeNull();
        var notifications = Substitute.For<INotificationService>();
        await job!(notifications, CancellationToken.None);
        await notifications.Received(1).SendClientReinviteAsync(client, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownClient_ThrowsKeyNotFound()
    {
        SetChannels("whatsapp");

        var act = () => _handler.Handle(new ReinviteClientCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _queue.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Theory]
    [InlineData("email")]
    [InlineData(null)]
    public async Task Handle_WhatsAppChannelOff_RefusesWithoutMarkingTheClientInvited(string? channels)
    {
        _settings.GetAsync("notifications:channels", Arg.Any<CancellationToken>()).Returns(channels);
        var client = StoredClient();

        var act = () => _handler.Handle(new ReinviteClientCommand(client.Id), CancellationToken.None);

        // Sem o canal nada seria enviado: marcar o convite só esconderia o cliente da lista
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(ReinviteClientCommandHandler.WhatsAppDisabledMessage);
        client.LastInviteAt.Should().BeNull();
        _queue.DidNotReceiveWithAnyArgs().Enqueue(default!);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvitedInTheLast10Days_RefusesASecondInvite()
    {
        SetChannels("whatsapp");
        var client = StoredClient();
        client.MarkInvited(_clock.ShopNow.AddDays(-2));

        var act = () => _handler.Handle(new ReinviteClientCommand(client.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*últimos 10 dias*");
        client.LastInviteAt.Should().Be(_clock.ShopNow.AddDays(-2));
        _queue.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }
}
