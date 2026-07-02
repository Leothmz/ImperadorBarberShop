namespace ImperadorBarberShop.Application.Interfaces;

public enum WhatsAppConnectionStatus { Connected, Disconnected, QrRequired }

public record WhatsAppStatus(WhatsAppConnectionStatus Status, string? PhoneNumber);

public record WhatsAppQr(string QrCode);

public interface IWhatsAppService
{
    Task SendAsync(string phone, string message, CancellationToken ct = default);
    Task<WhatsAppStatus> GetStatusAsync(CancellationToken ct = default);
    Task<WhatsAppQr> GetQrCodeAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
