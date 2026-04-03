using FluentValidation;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Barbers;

public record UpdateBarberAvailabilityCommand(
    Guid BarberId,
    List<AvailabilitySlotInput> Availability) : IRequest;

public class UpdateBarberAvailabilityCommandValidator : AbstractValidator<UpdateBarberAvailabilityCommand>
{
    public UpdateBarberAvailabilityCommandValidator()
    {
        RuleFor(x => x.BarberId).NotEmpty();
        RuleFor(x => x.Availability).NotNull();
        RuleForEach(x => x.Availability).ChildRules(slot =>
        {
            slot.RuleFor(s => s.EndTime).GreaterThan(s => s.StartTime)
                .WithMessage("EndTime must be after StartTime.");
        });
        RuleFor(x => x.Availability)
            .Must(slots => slots.Select(s => s.DayOfWeek).Distinct().Count() == slots.Count)
            .WithMessage("Duplicate DayOfWeek entries are not allowed.");
    }
}

public class UpdateBarberAvailabilityCommandHandler : IRequestHandler<UpdateBarberAvailabilityCommand>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBarberAvailabilityCommandHandler(
        IBarberRepository barberRepository,
        IBarberAvailabilityRepository availabilityRepository,
        IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _availabilityRepository = availabilityRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateBarberAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken);
        if (barber is null)
            throw new KeyNotFoundException($"Barber '{request.BarberId}' not found.");

        await _availabilityRepository.DeleteByBarberIdAsync(request.BarberId, cancellationToken);

        var newAvailabilities = request.Availability
            .Select(a => BarberAvailability.Create(request.BarberId, a.DayOfWeek, a.StartTime, a.EndTime))
            .ToList();

        if (newAvailabilities.Count > 0)
            await _availabilityRepository.AddRangeAsync(newAvailabilities, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
