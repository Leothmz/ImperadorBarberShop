using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Auth;

public record AvailabilitySlotInput(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public record RegisterBarberCommand(
    string Name,
    string Email,
    string Password,
    List<AvailabilitySlotInput> Availability) : IRequest<Guid>;

public class RegisterBarberCommandValidator : AbstractValidator<RegisterBarberCommand>
{
    public RegisterBarberCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
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

public class RegisterBarberCommandHandler : IRequestHandler<RegisterBarberCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterBarberCommandHandler(
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

    public async Task<Guid> Handle(RegisterBarberCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.CreateBarber(request.Name, request.Email, passwordHash);
        await _userRepository.AddAsync(user, cancellationToken);

        var barber = Barber.Create(user.Id);
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
