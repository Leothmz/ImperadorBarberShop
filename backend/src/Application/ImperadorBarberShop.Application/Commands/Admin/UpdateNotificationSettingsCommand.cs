using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record UpdateNotificationSettingsCommand(
    List<string> Channels,
    int ReminderMinutesBefore,
    string? NotificationPhone) : IRequest;

public class UpdateNotificationSettingsCommandValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    private static readonly HashSet<string> ValidChannels =
        new(StringComparer.OrdinalIgnoreCase) { "email", "whatsapp" };

    public UpdateNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.Channels)
            .NotEmpty()
            .Must(channels => channels.All(c => ValidChannels.Contains(c)))
            .WithMessage("Channels must only contain 'email' and/or 'whatsapp'.");

        RuleFor(x => x.ReminderMinutesBefore)
            .InclusiveBetween(5, 1440);

        RuleFor(x => x.NotificationPhone)
            .Matches(@"^\+55\d{11}$")
            .When(x => !string.IsNullOrEmpty(x.NotificationPhone))
            .WithMessage("NotificationPhone must be in the format +55DDDXXXXXXXXX.");
    }
}

public class UpdateNotificationSettingsCommandHandler : IRequestHandler<UpdateNotificationSettingsCommand>
{
    private readonly IAppSettingsRepository _settings;

    public UpdateNotificationSettingsCommandHandler(IAppSettingsRepository settings) => _settings = settings;

    public async Task Handle(UpdateNotificationSettingsCommand request, CancellationToken ct)
    {
        var validChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "whatsapp" };
        var filtered = request.Channels
            .Where(c => validChannels.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _settings.SetAsync("notifications:channels", string.Join(",", filtered), ct);
        await _settings.SetAsync("notifications:reminderMinutesBefore",
            request.ReminderMinutesBefore.ToString(), ct);

        if (request.NotificationPhone is not null)
            await _settings.SetAsync("whatsapp:notificationPhone", request.NotificationPhone, ct);
    }
}
