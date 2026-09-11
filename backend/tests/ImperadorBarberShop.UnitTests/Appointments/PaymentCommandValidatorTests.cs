using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.UnitTests.Appointments;

/// <summary>
/// Combinação inválida é recusada no servidor, antes do handler: o validador vira 400 por campo,
/// tanto ao concluir quanto ao registrar o pagamento depois.
/// </summary>
public class PaymentCommandValidatorTests
{
    private readonly CompleteAppointmentCommandValidator _complete = new();
    private readonly UpdatePaymentMethodCommandValidator _update = new();

    public static TheoryData<PaymentMethod, PlanKind?, PaymentMethod?, decimal?, string> InvalidPayloads => new()
    {
        { PaymentMethod.Plano, null, null, null, "PlanKind" },
        { PaymentMethod.Plano, PlanKind.Pagamento, null, 50m, "PlanTender" },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Plano, 50m, "PlanTender" },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, null, "ChargedAmount" },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, -1m, "ChargedAmount" },
        { PaymentMethod.Plano, PlanKind.Recorrencia, PaymentMethod.Dinheiro, null, "PlanTender" },
        { PaymentMethod.Plano, PlanKind.Recorrencia, null, 10m, "ChargedAmount" },
        { PaymentMethod.Pix, PlanKind.Recorrencia, null, null, "PlanKind" },
        { PaymentMethod.Cartão, null, null, 35m, "ChargedAmount" },
        { (PaymentMethod)42, null, null, null, "PaymentMethod" },
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public void Complete_InvalidPayload_FailsOnTheField(
        PaymentMethod method, PlanKind? kind, PaymentMethod? tender, decimal? amount, string field)
    {
        var result = _complete.Validate(new CompleteAppointmentCommand(Guid.NewGuid(), null, method, kind, tender, amount));

        result.Errors.Should().Contain(e => e.PropertyName == field);
    }

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public void UpdatePayment_InvalidPayload_FailsOnTheField(
        PaymentMethod method, PlanKind? kind, PaymentMethod? tender, decimal? amount, string field)
    {
        var result = _update.Validate(new UpdatePaymentMethodCommand(Guid.NewGuid(), method, null, kind, tender, amount));

        result.Errors.Should().Contain(e => e.PropertyName == field);
    }

    [Fact]
    public void Complete_WithoutMethodButWithPlanData_IsRefused()
    {
        var result = _complete.Validate(
            new CompleteAppointmentCommand(Guid.NewGuid(), null, null, PlanKind.Pagamento, PaymentMethod.Pix, 50m));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("PaymentMethod");
    }

    [Fact]
    public void Complete_WithoutAnyPayment_IsValid()
        => _complete.Validate(new CompleteAppointmentCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

    public static TheoryData<PaymentMethod, PlanKind?, PaymentMethod?, decimal?> ValidPayloads => new()
    {
        { PaymentMethod.Dinheiro, null, null, null },
        { PaymentMethod.Cartão, null, null, null },
        { PaymentMethod.Pix, null, null, null },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, 120m },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Dinheiro, 0m },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Cartão, 89.90m },
        { PaymentMethod.Plano, PlanKind.Recorrencia, null, null },
        { PaymentMethod.Plano, PlanKind.Recorrencia, null, 0m },
    };

    [Theory]
    [MemberData(nameof(ValidPayloads))]
    public void ValidPayload_PassesBothValidators(
        PaymentMethod method, PlanKind? kind, PaymentMethod? tender, decimal? amount)
    {
        _complete.Validate(new CompleteAppointmentCommand(Guid.NewGuid(), null, method, kind, tender, amount))
            .IsValid.Should().BeTrue();
        _update.Validate(new UpdatePaymentMethodCommand(Guid.NewGuid(), method, null, kind, tender, amount))
            .IsValid.Should().BeTrue();
    }
}
