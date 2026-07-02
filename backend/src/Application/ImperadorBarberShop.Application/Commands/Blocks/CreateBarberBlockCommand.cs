using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Blocks;

public record CreateBarberBlockCommand(
    Guid BarberId,
    DateTime StartsAt,
    DateTime EndsAt,
    string? Description,
    bool IsRecurring,
    int? RecurrenceDays,
    DateTime? RecurrenceEndsAt) : IRequest<Guid>;

public class CreateBarberBlockCommandValidator : AbstractValidator<CreateBarberBlockCommand>
{
    public CreateBarberBlockCommandValidator()
    {
        RuleFor(x => x.BarberId).NotEmpty();
        RuleFor(x => x.StartsAt).NotEmpty();
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt)
            .WithMessage("EndsAt must be after StartsAt.");
        RuleFor(x => x.Description).MaximumLength(200).When(x => x.Description is not null);
        When(x => x.IsRecurring, () =>
        {
            RuleFor(x => x.RecurrenceDays).NotNull()
                .InclusiveBetween(1, 127)
                .WithMessage("RecurrenceDays must be between 1 and 127 for recurring blocks.");
        });
        When(x => !x.IsRecurring, () =>
        {
            RuleFor(x => x.RecurrenceDays).Null()
                .WithMessage("RecurrenceDays must be null for non-recurring blocks.");
        });
    }
}

public class CreateBarberBlockCommandHandler : IRequestHandler<CreateBarberBlockCommand, Guid>
{
    private readonly IBarberBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateBarberBlockCommandHandler(IBarberBlockRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateBarberBlockCommand request, CancellationToken cancellationToken)
    {
        var block = BarberBlock.Create(
            request.BarberId,
            request.StartsAt,
            request.EndsAt,
            request.Description,
            request.IsRecurring,
            request.RecurrenceDays,
            request.RecurrenceEndsAt);

        await _repo.AddAsync(block, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return block.Id;
    }
}
