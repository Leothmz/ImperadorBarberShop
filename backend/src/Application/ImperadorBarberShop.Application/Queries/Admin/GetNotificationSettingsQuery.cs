using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;

public record NotificationSettingsDto(
    List<string> Channels,
    int ReminderMinutesBefore,
    string? NotificationPhone);

public class GetNotificationSettingsQueryHandler : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private readonly IAppSettingsRepository _settings;

    public GetNotificationSettingsQueryHandler(IAppSettingsRepository settings) => _settings = settings;

    public async Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery request, CancellationToken ct)
    {
        var channelsRaw = await _settings.GetAsync("notifications:channels", ct) ?? "email";
        var minutesStr  = await _settings.GetAsync("notifications:reminderMinutesBefore", ct) ?? "60";
        var phone       = await _settings.GetAsync("whatsapp:notificationPhone", ct);
        var channels    = channelsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var minutes = int.TryParse(minutesStr, out var m) ? m : 60;
        return new NotificationSettingsDto(channels, minutes, phone);
    }
}
