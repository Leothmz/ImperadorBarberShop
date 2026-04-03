using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Reviews;

public record CreateReviewCommand(
    Guid AppointmentId,
    Guid ClientId,
    int Rating,
    string? Comment) : IRequest<Guid>;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000).When(x => x.Comment is not null);
    }
}

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, Guid>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateReviewCommandHandler(
        IAppointmentRepository appointmentRepository,
        IReviewRepository reviewRepository,
        IBarberRepository barberRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _reviewRepository = reviewRepository;
        _barberRepository = barberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (appointment.ClientId != request.ClientId)
            throw new ForbiddenException("You are not authorized to review this appointment.");

        if (appointment.Status != AppointmentStatus.Completed)
            throw new InvalidOperationException("Can only review completed appointments.");

        var existing = await _reviewRepository.GetByAppointmentIdAsync(request.AppointmentId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("This appointment has already been reviewed.");

        var review = Review.Create(
            request.AppointmentId,
            request.ClientId,
            appointment.BarberId,
            request.Rating,
            request.Comment);

        await _reviewRepository.AddAsync(review, cancellationToken);

        // Persist the new review first so the average includes it
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Recalculate barber average rating after the review is persisted
        var updatedAverage = await _reviewRepository.GetAverageRatingByBarberIdAsync(appointment.BarberId, cancellationToken);
        var barber = await _barberRepository.GetByIdAsync(appointment.BarberId, cancellationToken);
        if (barber is not null)
        {
            barber.UpdateAverageRating(updatedAverage);
            await _barberRepository.UpdateAsync(barber, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return review.Id;
    }
}
