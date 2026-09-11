using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CompleteAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IClientRepository _clientRepository = Substitute.For<IClientRepository>();
    private readonly INotificationQueue _notifications = Substitute.For<INotificationQueue>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CompleteAppointmentCommandHandler _handler;

    public CompleteAppointmentCommandHandlerTests()
    {
        _handler = new CompleteAppointmentCommandHandler(_appointmentRepository, _clientRepository, _notifications, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidComplete_CompletesAppointment()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Service.Create("Corte", "Desc", 30, 35m) });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbiddenException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Service.Create("Corte", "Desc", 30, 35m) });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AdminWithoutBarberId_CompletesAnyBarbersAppointment()
    {
        // RequesterBarberId nulo = admin: conclui o atendimento de qualquer barbeiro.
        var otherBarberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", otherBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Service.Create("Corte", "Desc", 30, 35m) });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, null, PaymentMethod.Pix), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Service.Create("Corte", "Desc", 30, 35m) });
        appointment.Complete();
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithPaymentMethod_SetsPaymentOnCompletion()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId, PaymentMethod.Pix), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public async Task Handle_PlanPayment_RecordsTheChargedAmountAndTender()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(
            new CompleteAppointmentCommand(appointment.Id, barberId, PaymentMethod.Plano, PlanKind.Pagamento, PaymentMethod.Pix, 120m),
            CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentMethod.Should().Be(PaymentMethod.Plano);
        appointment.PlanKind.Should().Be(PlanKind.Pagamento);
        appointment.PlanTender.Should().Be(PaymentMethod.Pix);
        appointment.ChargedAmount.Should().Be(120m);
        appointment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AdminPlanRecurrence_CompletesAtZeroAndStillCountsTheVisit()
    {
        var (appointment, client) = LinkedAppointment(Guid.NewGuid(), new DateTime(2026, 9, 10, 10, 0, 0));

        await _handler.Handle(
            new CompleteAppointmentCommand(appointment.Id, null, PaymentMethod.Plano, PlanKind.Recorrencia),
            CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PlanKind.Should().Be(PlanKind.Recorrencia);
        appointment.EffectiveAmount.Should().Be(0m);
        appointment.PaidAt.Should().BeNull();
        // Visita coberta pelo plano é visita: a recorrência de clientes continua medindo
        client.VisitCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WrongBarberWithPlanPayload_ThrowsForbiddenAndChangesNothing()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(
            new CompleteAppointmentCommand(appointment.Id, Guid.NewGuid(), PaymentMethod.Plano, PlanKind.Recorrencia),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        appointment.Status.Should().Be(AppointmentStatus.Accepted);
        appointment.PaymentMethod.Should().BeNull();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_InvalidPlanPayload_IsRefusedBeforeTouchingTheAppointment()
    {
        // O ValidationBehavior não cobre comandos sem retorno: o próprio handler recusa (400)
        var act = () => _handler.Handle(
            new CompleteAppointmentCommand(Guid.NewGuid(), null, PaymentMethod.Plano, PlanKind.Recorrencia, PaymentMethod.Pix),
            CancellationToken.None);

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "PlanTender");
        await _appointmentRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    private (Appointment appointment, Client client) LinkedAppointment(Guid barberId, DateTime scheduledAt)
    {
        var client = Client.Create("João", BrazilianPhone.Parse("+5511999990000"), DateTime.UtcNow.AddDays(-60));
        var appointment = Appointment.Create("João", client.Phone, barberId, scheduledAt, 30, null,
            [Service.Create("Corte", "Desc", 30, 35m)], client.Id);
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _clientRepository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return (appointment, client);
    }

    [Fact]
    public async Task Handle_LinkedClient_CountsTheVisitAtTheScheduledTime()
    {
        var barberId = Guid.NewGuid();
        var scheduledAt = new DateTime(2026, 9, 10, 10, 0, 0);
        var (appointment, client) = LinkedAppointment(barberId, scheduledAt);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        client.VisitCount.Should().Be(1);
        client.LastVisitAt.Should().Be(scheduledAt);
        await _clientRepository.Received(1).UpdateAsync(client, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CompletionRejected_DoesNotCountAVisit()
    {
        var barberId = Guid.NewGuid();
        var (appointment, client) = LinkedAppointment(barberId, new DateTime(2026, 9, 10, 10, 0, 0));
        appointment.Cancel();

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        // Cancelado não vira visita: só a conclusão conta
        await act.Should().ThrowAsync<InvalidOperationException>();
        client.VisitCount.Should().Be(0);
        client.LastVisitAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LegacyAppointmentWithoutClient_CompletesWithoutTouchingClients()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "telefone ilegível", barberId, DateTime.UtcNow.AddDays(1), 30, null,
            [Service.Create("Corte", "Desc", 30, 35m)]);
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        await _clientRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }
}
