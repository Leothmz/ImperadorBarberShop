using FluentValidation;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record CreateBarberByAdminCommand(
    string Name,
    string Email,
    string Password,
    List<AvailabilitySlotInput> Availability,
    string? PhotoUrl) : IRequest<Guid>;

public class CreateBarberByAdminCommandValidator : AbstractValidator<CreateBarberByAdminCommand>
{
    public CreateBarberByAdminCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleForEach(x => x.Availability).ChildRules(slot =>
            slot.RuleFor(s => s.EndTime).GreaterThan(s => s.StartTime)
                .WithMessage("EndTime must be after StartTime."));
    }
}

public class CreateBarberByAdminCommandHandler : IRequestHandler<CreateBarberByAdminCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBarberByAdminCommandHandler(
        IUserRepository userRepository,
        IBarberRepository barberRepository,
        IBarberAvailabilityRepository availabilityRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _barberRepository = barberRepository;
        _availabilityRepository = availabilityRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateBarberByAdminCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");

        var hash = _passwordHasher.Hash(request.Password);
        var user = User.CreateBarber(request.Name, request.Email, hash);
        await _userRepository.AddAsync(user, cancellationToken);

        var barber = Barber.Create(user.Id);
        if (request.PhotoUrl is not null)
            barber.UpdatePhoto(request.PhotoUrl);
        await _barberRepository.AddAsync(barber, cancellationToken);

        var availabilities = request.Availability
            .Select(a => BarberAvailability.Create(barber.Id, a.DayOfWeek, a.StartTime, a.EndTime))
            .ToList();
        if (availabilities.Count > 0)
            await _availabilityRepository.AddRangeAsync(availabilities, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
