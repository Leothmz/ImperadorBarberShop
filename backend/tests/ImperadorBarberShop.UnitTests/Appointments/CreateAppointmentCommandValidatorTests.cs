using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CreateAppointmentCommandValidatorTests
{
    // Servidor em UTC às 21:58; na barbearia são 18:58
    private readonly CreateAppointmentCommandValidator _validator = new(new FixedShopClock(FixedShopClock.EveningUtc));

    private static CreateAppointmentCommand CommandAt(DateTime scheduledAt) => new(
        "João", "+5511999990000", Guid.NewGuid(), scheduledAt, new List<Guid> { Guid.NewGuid() }, null);

    [Fact]
    public void Validate_OneHourAheadInShopTime_IsAccepted()
    {
        // Reprodução do relatório: 20:00 é 1h no futuro para a barbearia, mas ficava
        // "no passado" contra DateTime.UtcNow (21:58) e voltava 400.
        var result = _validator.Validate(CommandAt(new DateTime(2026, 9, 10, 20, 0, 0)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EarlierTodayInShopTime_IsRejectedInPortuguese()
    {
        var result = _validator.Validate(CommandAt(new DateTime(2026, 9, 10, 18, 30, 0)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateAppointmentCommand.ScheduledAt))
            .Which.ErrorMessage.Should().Be("O horário escolhido já passou. Escolha um horário futuro.");
    }
}
