using FluentValidation;
using ImperadorBarberShop.Application.Common;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Clients;

/// <summary>Convida de volta, pelo WhatsApp, um cliente que está sumindo.</summary>
public record ReinviteClientCommand(Guid ClientId) : IRequest;

public class ReinviteClientCommandValidator : AbstractValidator<ReinviteClientCommand>
{
    public ReinviteClientCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
    }
}

public class ReinviteClientCommandHandler : IRequestHandler<ReinviteClientCommand>
{
    public const string WhatsAppDisabledMessage =
        "O envio por WhatsApp está desativado. Ative o canal em WhatsApp → Notificações para convidar clientes.";

    private readonly IClientRepository _clientRepository;
    private readonly IAppSettingsRepository _settings;
    private readonly INotificationQueue _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ReinviteClientCommandHandler(
        IClientRepository clientRepository,
        IAppSettingsRepository settings,
        INotificationQueue notifications,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _clientRepository = clientRepository;
        _settings = settings;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task Handle(ReinviteClientCommand request, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByIdAsync(request.ClientId, cancellationToken);
        if (client is null)
            throw new KeyNotFoundException($"Client '{request.ClientId}' not found.");

        // Com o canal desligado o NotificationService descartaria o envio em silêncio, e o
        // cliente sumiria da lista por 10 dias sem ter recebido nada
        var channels = NotificationChannels.Parse(
            await _settings.GetAsync(NotificationChannels.SettingKey, cancellationToken));
        if (!channels.Contains(NotificationChannels.WhatsApp))
            throw new InvalidOperationException(WhatsAppDisabledMessage);

        client.MarkInvited(_clock.GetLocalNow().DateTime);
        await _clientRepository.UpdateAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fora do ciclo da requisição, como as demais notificações
        _notifications.Enqueue((n, ct) => n.SendClientReinviteAsync(client, ct));
    }
}
