using ImperadorBarberShop.Application.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record DisconnectWhatsAppCommand : IRequest;

public class DisconnectWhatsAppCommandHandler : IRequestHandler<DisconnectWhatsAppCommand>
{
    private readonly IWhatsAppService _whatsApp;

    public DisconnectWhatsAppCommandHandler(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    public async Task Handle(DisconnectWhatsAppCommand request, CancellationToken ct)
        => await _whatsApp.DisconnectAsync(ct);
}
