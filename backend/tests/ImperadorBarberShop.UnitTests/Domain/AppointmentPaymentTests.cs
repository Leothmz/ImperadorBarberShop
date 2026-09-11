using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.ValueObjects;

namespace ImperadorBarberShop.UnitTests.Domain;

/// <summary>As combinações de pagamento aceitas, e as recusadas, antes de chegar ao agendamento.</summary>
public class AppointmentPaymentTests
{
    [Theory]
    [InlineData(PaymentMethod.Dinheiro)]
    [InlineData(PaymentMethod.Cartão)]
    [InlineData(PaymentMethod.Pix)]
    public void Normal_CarriesOnlyTheMethod(PaymentMethod method)
    {
        var payment = AppointmentPayment.Normal(method);

        payment.Method.Should().Be(method);
        payment.PlanKind.Should().BeNull();
        payment.PlanTender.Should().BeNull();
        payment.ChargedAmount.Should().BeNull();
    }

    [Fact]
    public void Normal_Plano_IsRejected_BecauseThePlanNeedsItsKind()
    {
        var act = () => AppointmentPayment.Normal(PaymentMethod.Plano);

        act.Should().Throw<ArgumentException>().WithMessage("*Pagamento ou Recorrência*");
    }

    [Theory]
    [InlineData(PaymentMethod.Pix)]
    [InlineData(PaymentMethod.Dinheiro)]
    [InlineData(PaymentMethod.Cartão)]
    public void PlanPayment_StoresTheTypedAmountAndTender(PaymentMethod tender)
    {
        var payment = AppointmentPayment.PlanPayment(120.50m, tender);

        payment.Method.Should().Be(PaymentMethod.Plano);
        payment.PlanKind.Should().Be(PlanKind.Pagamento);
        payment.PlanTender.Should().Be(tender);
        payment.ChargedAmount.Should().Be(120.50m);
    }

    [Fact]
    public void PlanPayment_ZeroIsAValidAmount()
        => AppointmentPayment.PlanPayment(0m, PaymentMethod.Pix).ChargedAmount.Should().Be(0m);

    [Fact]
    public void PlanRecurrence_ChargesZeroWithoutTender()
    {
        var payment = AppointmentPayment.PlanRecurrence();

        payment.Method.Should().Be(PaymentMethod.Plano);
        payment.PlanKind.Should().Be(PlanKind.Recorrencia);
        payment.PlanTender.Should().BeNull();
        payment.ChargedAmount.Should().Be(0m);
    }

    [Fact]
    public void Create_RecurrenceWithExplicitZero_IsTheSameAsWithoutAmount()
        => AppointmentPayment.Create(PaymentMethod.Plano, PlanKind.Recorrencia, chargedAmount: 0m)
            .Should().Be(AppointmentPayment.PlanRecurrence());

    public static TheoryData<PaymentMethod, PlanKind?, PaymentMethod?, decimal?, string> InvalidCombinations => new()
    {
        // Método normal não carrega nada do plano
        { PaymentMethod.Pix, PlanKind.Pagamento, null, null, PaymentError.PlanKindField },
        { PaymentMethod.Dinheiro, null, PaymentMethod.Pix, null, PaymentError.PlanTenderField },
        { PaymentMethod.Cartão, null, null, 50m, PaymentError.ChargedAmountField },
        // Plano sem tipo, ou com tipo desconhecido
        { PaymentMethod.Plano, null, null, null, PaymentError.PlanKindField },
        { PaymentMethod.Plano, (PlanKind)9, null, null, PaymentError.PlanKindField },
        // Pagamento: valor e forma obrigatórios, valor não negativo com centavos no máximo
        { PaymentMethod.Plano, PlanKind.Pagamento, null, 50m, PaymentError.PlanTenderField },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, null, PaymentError.ChargedAmountField },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, -0.01m, PaymentError.ChargedAmountField },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, 10.005m, PaymentError.ChargedAmountField },
        { PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Plano, 50m, PaymentError.PlanTenderField },
        { PaymentMethod.Plano, PlanKind.Pagamento, (PaymentMethod)9, 50m, PaymentError.PlanTenderField },
        // Recorrência não cobra e não tem forma de pagamento
        { PaymentMethod.Plano, PlanKind.Recorrencia, PaymentMethod.Pix, null, PaymentError.PlanTenderField },
        { PaymentMethod.Plano, PlanKind.Recorrencia, null, 35m, PaymentError.ChargedAmountField },
        // Método desconhecido
        { (PaymentMethod)9, null, null, null, PaymentError.PaymentMethodField },
    };

    [Theory]
    [MemberData(nameof(InvalidCombinations))]
    public void InvalidCombination_IsReportedOnItsFieldAndRefused(
        PaymentMethod method, PlanKind? planKind, PaymentMethod? planTender, decimal? chargedAmount, string field)
    {
        AppointmentPayment.Validate(method, planKind, planTender, chargedAmount)
            .Should().Contain(e => e.Field == field);

        var act = () => AppointmentPayment.Create(method, planKind, planTender, chargedAmount);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_ReportsEveryBrokenFieldAtOnce()
        => AppointmentPayment.Validate(PaymentMethod.Plano, PlanKind.Pagamento, null, null)
            .Select(e => e.Field)
            .Should().BeEquivalentTo([PaymentError.PlanTenderField, PaymentError.ChargedAmountField]);
}

/// <summary>O pagamento aplicado ao agendamento: valor efetivo, dados do plano e PaidAt.</summary>
public class AppointmentPlanPaymentTests
{
    private static Appointment CorteEBarba()
        => Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 50, null,
            [Service.Create("Corte", "Desc", 30, 35m), Service.Create("Barba", "Desc", 20, 25m)]);

    private static Appointment CompletedCorteEBarba(AppointmentPayment? payment = null)
    {
        var appointment = CorteEBarba();
        appointment.Complete(payment);
        return appointment;
    }

    [Fact]
    public void EffectiveAmount_OutsideThePlan_IsTheSumOfTheBookedPrices()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.Normal(PaymentMethod.Pix));

        appointment.ChargedAmount.Should().BeNull();
        appointment.EffectiveAmount.Should().Be(60m);
        appointment.IsPlan.Should().BeFalse();
    }

    [Fact]
    public void EffectiveAmount_WithoutRegisteredPayment_IsTheSumOfTheBookedPrices()
        => CompletedCorteEBarba().EffectiveAmount.Should().Be(60m);

    [Fact]
    public void Complete_WithPlanPayment_RecordsThePlanAndPaysAtTheVisit()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.PlanPayment(150m, PaymentMethod.Cartão));

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentMethod.Should().Be(PaymentMethod.Plano);
        appointment.PlanKind.Should().Be(PlanKind.Pagamento);
        appointment.PlanTender.Should().Be(PaymentMethod.Cartão);
        appointment.ChargedAmount.Should().Be(150m);
        appointment.EffectiveAmount.Should().Be(150m);
        appointment.PaidAt.Should().NotBeNull();
        appointment.IsPlan.Should().BeTrue();
    }

    [Fact]
    public void Complete_WithPlanRecurrence_IsCompletedAtZeroWithoutInventingAPayment()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.PlanRecurrence());

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentMethod.Should().Be(PaymentMethod.Plano);
        appointment.PlanKind.Should().Be(PlanKind.Recorrencia);
        appointment.PlanTender.Should().BeNull();
        appointment.ChargedAmount.Should().Be(0m);
        appointment.EffectiveAmount.Should().Be(0m);
        appointment.PaidAt.Should().BeNull("nada foi pago nesta visita");
    }

    [Fact]
    public void PlanRecurrence_KeepsTheServicesAndTheirBookedPrices()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.PlanRecurrence());

        appointment.AppointmentServices.Select(s => s.UnitPrice).Should().Equal(35m, 25m);
    }

    [Fact]
    public void SetPayment_LaterPlanPayment_OnACompletedAppointment()
    {
        var appointment = CompletedCorteEBarba();

        appointment.SetPayment(AppointmentPayment.PlanPayment(90m, PaymentMethod.Dinheiro));

        appointment.PaymentMethod.Should().Be(PaymentMethod.Plano);
        appointment.PlanKind.Should().Be(PlanKind.Pagamento);
        appointment.PlanTender.Should().Be(PaymentMethod.Dinheiro);
        appointment.EffectiveAmount.Should().Be(90m);
        appointment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void SetPayment_LaterPlanRecurrence_OnACompletedAppointment()
    {
        var appointment = CompletedCorteEBarba();

        appointment.SetPayment(AppointmentPayment.PlanRecurrence());

        appointment.PlanKind.Should().Be(PlanKind.Recorrencia);
        appointment.EffectiveAmount.Should().Be(0m);
        appointment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void SetPayment_BackToANormalMethod_ClearsThePlanAndRestoresTheServiceTotal()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.PlanPayment(150m, PaymentMethod.Pix));

        appointment.SetPayment(AppointmentPayment.Normal(PaymentMethod.Dinheiro));

        appointment.PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
        appointment.PlanKind.Should().BeNull();
        appointment.PlanTender.Should().BeNull();
        appointment.ChargedAmount.Should().BeNull();
        appointment.EffectiveAmount.Should().Be(60m);
        appointment.IsPlan.Should().BeFalse();
    }

    [Fact]
    public void SetPayment_FromRecurrenceToANormalMethod_DatesThePaymentNow()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.PlanRecurrence());

        appointment.SetPayment(AppointmentPayment.Normal(PaymentMethod.Pix));

        appointment.PaidAt.Should().NotBeNull();
        appointment.EffectiveAmount.Should().Be(60m);
    }

    [Fact]
    public void SetPayment_FromAPaidMethodToRecurrence_DropsThePaymentDate()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.Normal(PaymentMethod.Pix));

        appointment.SetPayment(AppointmentPayment.PlanRecurrence());

        appointment.PaidAt.Should().BeNull();
    }

    [Fact]
    public void SetPayment_BetweenPaidMethods_KeepsTheFirstPaymentDate()
    {
        var appointment = CompletedCorteEBarba(AppointmentPayment.Normal(PaymentMethod.Pix));
        var paidAt = appointment.PaidAt;

        appointment.SetPayment(AppointmentPayment.PlanPayment(100m, PaymentMethod.Pix));

        appointment.PaidAt.Should().Be(paidAt);
    }

    [Fact]
    public void SetPayment_OnAnAcceptedAppointment_IsRefused()
    {
        var act = () => CorteEBarba().SetPayment(AppointmentPayment.PlanRecurrence());

        act.Should().Throw<InvalidOperationException>();
    }
}
