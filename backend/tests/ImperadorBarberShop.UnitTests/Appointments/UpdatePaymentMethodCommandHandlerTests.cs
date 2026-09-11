using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class UpdatePaymentMethodCommandHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly UpdatePaymentMethodCommandHandler _handler;

    public UpdatePaymentMethodCommandHandlerTests()
        => _handler = new UpdatePaymentMethodCommandHandler(_repo, _uow);

    private static Appointment MakeCompleted(Guid barberId)
    {
        var appt = Appointment.Create("João", "+55119", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_Admin_SetsPaymentMethod()
    {
        var barberId = Guid.NewGuid();
        var appt = MakeCompleted(barberId);
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Pix, null), CancellationToken.None);

        appt.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public async Task Handle_CorrectBarber_SetsPaymentMethod()
    {
        var barberId = Guid.NewGuid();
        var appt = MakeCompleted(barberId);
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Dinheiro, barberId), CancellationToken.None);

        appt.PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbidden()
    {
        var appt = MakeCompleted(Guid.NewGuid());
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        var act = () => _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Pix, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_CorrectBarber_RegistersAPlanPaymentLater()
    {
        var barberId = Guid.NewGuid();
        var appt = MakeCompleted(barberId);
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(
            new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Plano, barberId, PlanKind.Pagamento, PaymentMethod.Cartão, 99.90m),
            CancellationToken.None);

        appt.PaymentMethod.Should().Be(PaymentMethod.Plano);
        appt.PlanKind.Should().Be(PlanKind.Pagamento);
        appt.PlanTender.Should().Be(PaymentMethod.Cartão);
        appt.EffectiveAmount.Should().Be(99.90m);
        appt.PaidAt.Should().NotBeNull();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Admin_RegistersAPlanRecurrenceLater()
    {
        var appt = MakeCompleted(Guid.NewGuid());
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(
            new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Plano, null, PlanKind.Recorrencia),
            CancellationToken.None);

        appt.PlanKind.Should().Be(PlanKind.Recorrencia);
        appt.PlanTender.Should().BeNull();
        appt.ChargedAmount.Should().Be(0m);
        appt.PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NormalMethodAfterAPlan_ClearsThePlanFields()
    {
        var appt = MakeCompleted(Guid.NewGuid());
        appt.SetPayment(AppointmentPayment.PlanPayment(80m, PaymentMethod.Pix));
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        await _handler.Handle(new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Dinheiro, null), CancellationToken.None);

        appt.PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
        appt.PlanKind.Should().BeNull();
        appt.PlanTender.Should().BeNull();
        appt.ChargedAmount.Should().BeNull();
        appt.EffectiveAmount.Should().Be(35m);
    }

    [Fact]
    public async Task Handle_WrongBarberWithPlanPayload_ThrowsForbiddenAndChangesNothing()
    {
        var appt = MakeCompleted(Guid.NewGuid());
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        var act = () => _handler.Handle(
            new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Plano, Guid.NewGuid(), PlanKind.Pagamento, PaymentMethod.Pix, 50m),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        appt.PaymentMethod.Should().BeNull();
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_OnAnAcceptedAppointment_IsRefused()
    {
        var barberId = Guid.NewGuid();
        var appt = Appointment.Create("João", "+55119", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        _repo.GetByIdAsync(appt.Id, Arg.Any<CancellationToken>()).Returns(appt);

        var act = () => _handler.Handle(
            new UpdatePaymentMethodCommand(appt.Id, PaymentMethod.Plano, barberId, PlanKind.Recorrencia),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_InvalidPlanPayload_IsRefusedBeforeTouchingTheAppointment()
    {
        var act = () => _handler.Handle(
            new UpdatePaymentMethodCommand(Guid.NewGuid(), PaymentMethod.Plano, null, PlanKind.Pagamento, PaymentMethod.Pix, -10m),
            CancellationToken.None);

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "ChargedAmount");
        await _repo.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new UpdatePaymentMethodCommand(Guid.NewGuid(), PaymentMethod.Pix, null), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
