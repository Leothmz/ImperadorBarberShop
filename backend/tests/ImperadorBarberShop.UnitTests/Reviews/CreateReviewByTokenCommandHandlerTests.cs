using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Reviews;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Reviews;

public class CreateReviewByTokenCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IReviewRepository _reviewRepository = Substitute.For<IReviewRepository>();
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateReviewByTokenCommandHandler _handler;

    public CreateReviewByTokenCommandHandlerTests()
    {
        _handler = new CreateReviewByTokenCommandHandler(
            _appointmentRepository, _reviewRepository, _barberRepository, _unitOfWork);
    }

    private static Appointment CreateCompletedAppointment(Guid barberId)
    {
        var appt = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddHours(-2), 30, null, new[] { Guid.NewGuid() });
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_ValidReview_ReturnsReviewId()
    {
        var barberId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(barberId);
        var barber = Barber.Create(barberId);

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _reviewRepository.GetByAppointmentIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns((Review?)null);
        _reviewRepository.GetAverageRatingByBarberIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(4.5m);
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new CreateReviewByTokenCommand(appointment.AccessToken, 5, "Ótimo atendimento!");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _reviewRepository.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CreateReviewByTokenCommand("bogus", 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_AppointmentNotCompleted_ThrowsInvalidOperationException()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        // Status is Accepted, not Completed

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CreateReviewByTokenCommand(appointment.AccessToken, 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*completed*");
    }

    [Fact]
    public async Task Handle_DuplicateReview_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = CreateCompletedAppointment(barberId);
        var existingReview = Review.Create(appointment.Id, barberId, 4, null);

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _reviewRepository.GetByAppointmentIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(existingReview);

        var act = () => _handler.Handle(new CreateReviewByTokenCommand(appointment.AccessToken, 5, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been reviewed*");
    }
}
