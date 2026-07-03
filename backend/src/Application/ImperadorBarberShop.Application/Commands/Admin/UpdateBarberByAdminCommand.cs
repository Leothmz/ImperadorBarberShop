using FluentValidation;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record UpdateBarberByAdminCommand(
    Guid BarberId,
    string Name,
    string Email,
    string? Password,
    string? PhotoUrl,
    List<AvailabilitySlotInput> Availability) : IRequest;

public class UpdateBarberByAdminCommandValidator : AbstractValidator<UpdateBarberByAdminCommand>
{
    public UpdateBarberByAdminCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(100).When(x => x.Password is not null);
        RuleForEach(x => x.Availability).ChildRules(slot =>
            slot.RuleFor(s => s.EndTime).GreaterThan(s => s.StartTime)
                .WithMessage("EndTime must be after StartTime."));
    }
}

public class UpdateBarberByAdminCommandHandler : IRequestHandler<UpdateBarberByAdminCommand>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBarberByAdminCommandHandler(
        IBarberRepository barberRepository,
        IUserRepository userRepository,
        IBarberAvailabilityRepository availabilityRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _userRepository = userRepository;
        _availabilityRepository = availabilityRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateBarberByAdminCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken)
            ?? throw new KeyNotFoundException($"Barber {request.BarberId} not found.");

        var user = await _userRepository.GetByIdAsync(barber.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {barber.UserId} not found.");

        var emailOwner = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (emailOwner is not null && emailOwner.Id != user.Id)
            throw new InvalidOperationException($"Email '{request.Email}' is already in use.");

        user.UpdateProfile(request.Name, request.Email);
        if (request.Password is not null)
            user.UpdatePasswordHash(_passwordHasher.Hash(request.Password));
        await _userRepository.UpdateAsync(user, cancellationToken);

        if (request.PhotoUrl is not null)
            barber.UpdatePhoto(request.PhotoUrl);
        await _barberRepository.UpdateAsync(barber, cancellationToken);

        await _availabilityRepository.DeleteByBarberIdAsync(barber.Id, cancellationToken);
        var newAvailabilities = request.Availability
            .Select(a => BarberAvailability.Create(barber.Id, a.DayOfWeek, a.StartTime, a.EndTime))
            .ToList();
        if (newAvailabilities.Count > 0)
            await _availabilityRepository.AddRangeAsync(newAvailabilities, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
