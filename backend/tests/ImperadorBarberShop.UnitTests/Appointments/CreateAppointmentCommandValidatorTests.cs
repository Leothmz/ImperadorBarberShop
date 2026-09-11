using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.ValueObjects;

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

    private static CreateAppointmentCommand CommandWithPhone(string phone) => new(
        "João", phone, Guid.NewGuid(), new DateTime(2026, 9, 11, 10, 0, 0), new List<Guid> { Guid.NewGuid() }, null);

    [Theory]
    [InlineData("+5511999990000")]
    [InlineData("11 9 9999-0000")]
    [InlineData("11 9999-0000")]
    [InlineData("(11) 99999-0000")]
    [InlineData("011 99999-0000")]
    public void Validate_AnyParseableBrazilianMobile_IsAccepted(string phone)
    {
        _validator.Validate(CommandWithPhone(phone)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("99999")]
    [InlineData("+1 555 123 4567")]
    [InlineData("11 3333-4444")]
    public void Validate_UnparseablePhone_IsRejectedWithAClearMessage(string phone)
    {
        var result = _validator.Validate(CommandWithPhone(phone));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateAppointmentCommand.ClientPhone))
            .Which.ErrorMessage.Should().Be(BrazilianPhone.InvalidMessage);
    }
}
