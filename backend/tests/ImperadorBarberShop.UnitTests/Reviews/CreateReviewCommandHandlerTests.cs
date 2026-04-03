using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Reviews;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Reviews;

public class CreateReviewCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IReviewRepository _reviewRepository = Substitute.For<IReviewRepository>();
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateReviewCommandHandler _handler;

    public CreateReviewCommandHandlerTests()
    {
        _handler = new CreateReviewCommandHandler(
            _appointmentRepository, _reviewRepository, _barberRepository, _unitOfWork);
    }

    private static Appointment CreateCompletedAppointment(Guid clientId, Guid barberId)
    {
        var appt = Appointment.Create(clientId, barberId, DateTime.UtcNow.AddHours(-2), 30, null, new[] { Guid.NewGuid() });
        appt.Accept();
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_ValidReview_ReturnsReviewId()
    {
        var clientId = Guid.NewGuid();
        var barberId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(clientId, barberId);
        var barber = Barber.Create(barberId);

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _reviewRepository.GetByAppointmentIdAsync(appointment.Id, Arg.Any<CancellationToken>())
            .Returns((Review?)null);
        _reviewRepository.GetAverageRatingByBarberIdAsync(barberId, Arg.Any<CancellationToken>())
            .Returns(4.5m);
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new CreateReviewCommand(appointment.Id, clientId, 5, "Ótimo atendimento!");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _reviewRepository.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(
            new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongClient_ThrowsUnauthorizedAccessException()
    {
        var realClientId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(realClientId, Guid.NewGuid());
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(
            new CreateReviewCommand(appointment.Id, Guid.NewGuid(), 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AppointmentNotCompleted_ThrowsInvalidOperationException()
    {
        var clientId = Guid.NewGuid();
        var appointment = Appointment.Create(
            clientId, Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        // Status is Pending, not Completed

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(
            new CreateReviewCommand(appointment.Id, clientId, 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*completed*");
    }

    [Fact]
    public async Task Handle_DuplicateReview_ThrowsInvalidOperationException()
    {
        var clientId = Guid.NewGuid();
        var barberId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(clientId, barberId);
        var existingReview = Review.Create(appointment.Id, clientId, barberId, 4, null);

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _reviewRepository.GetByAppointmentIdAsync(appointment.Id, Arg.Any<CancellationToken>())
            .Returns(existingReview);

        var act = () => _handler.Handle(
            new CreateReviewCommand(appointment.Id, clientId, 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been reviewed*");
    }
}
