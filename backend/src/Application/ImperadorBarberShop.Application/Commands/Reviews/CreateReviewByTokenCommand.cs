using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Reviews;

public record CreateReviewByTokenCommand(
    string AccessToken,
    int Rating,
    string? Comment) : IRequest<Guid>;

public class CreateReviewByTokenCommandValidator : AbstractValidator<CreateReviewByTokenCommand>
{
    public CreateReviewByTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000).When(x => x.Comment is not null);
    }
}

public class CreateReviewByTokenCommandHandler : IRequestHandler<CreateReviewByTokenCommand, Guid>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateReviewByTokenCommandHandler(
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

    public async Task<Guid> Handle(CreateReviewByTokenCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        if (appointment.Status != AppointmentStatus.Completed)
            throw new InvalidOperationException("Can only review completed appointments.");

        var existing = await _reviewRepository.GetByAppointmentIdAsync(appointment.Id, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("This appointment has already been reviewed.");

        var review = Review.Create(appointment.Id, appointment.BarberId, request.Rating, request.Comment);

        await _reviewRepository.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
