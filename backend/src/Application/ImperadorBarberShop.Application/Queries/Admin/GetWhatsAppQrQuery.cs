using ImperadorBarberShop.Application.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetWhatsAppQrQuery : IRequest<WhatsAppQrDto>;

public record WhatsAppQrDto(string QrCode);

public class GetWhatsAppQrQueryHandler : IRequestHandler<GetWhatsAppQrQuery, WhatsAppQrDto>
{
    private readonly IWhatsAppService _whatsApp;

    public GetWhatsAppQrQueryHandler(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    public async Task<WhatsAppQrDto> Handle(GetWhatsAppQrQuery request, CancellationToken ct)
    {
        var qr = await _whatsApp.GetQrCodeAsync(ct);
        return new WhatsAppQrDto(qr.QrCode);
    }
}
