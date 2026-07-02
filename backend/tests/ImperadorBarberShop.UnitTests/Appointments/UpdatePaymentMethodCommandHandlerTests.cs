using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
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
        var appt = Appointment.Create("João", "+55119", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
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
    public async Task Handle_NotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new UpdatePaymentMethodCommand(Guid.NewGuid(), PaymentMethod.Pix, null), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
