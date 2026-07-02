using ImperadorBarberShop.Application.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetWhatsAppStatusQuery : IRequest<WhatsAppStatusDto>;

public record WhatsAppStatusDto(string Status, string? PhoneNumber);

public class GetWhatsAppStatusQueryHandler : IRequestHandler<GetWhatsAppStatusQuery, WhatsAppStatusDto>
{
    private readonly IWhatsAppService _whatsApp;

    public GetWhatsAppStatusQueryHandler(IWhatsAppService whatsApp) => _whatsApp = whatsApp;

    public async Task<WhatsAppStatusDto> Handle(GetWhatsAppStatusQuery request, CancellationToken ct)
    {
        try
        {
            var status = await _whatsApp.GetStatusAsync(ct);
            var statusStr = status.Status switch
            {
                WhatsAppConnectionStatus.Connected  => "connected",
                WhatsAppConnectionStatus.QrRequired => "qr_required",
                _                                   => "disconnected"
            };
            return new WhatsAppStatusDto(statusStr, status.PhoneNumber);
        }
        catch
        {
            return new WhatsAppStatusDto("disconnected", null);
        }
    }
}
