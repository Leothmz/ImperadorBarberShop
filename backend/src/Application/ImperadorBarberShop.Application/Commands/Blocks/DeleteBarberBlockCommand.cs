using FluentValidation;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Blocks;

public record DeleteBarberBlockCommand(Guid BlockId, Guid BarberId) : IRequest;

public class DeleteBarberBlockCommandValidator : AbstractValidator<DeleteBarberBlockCommand>
{
    public DeleteBarberBlockCommandValidator()
    {
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
    }
}

public class DeleteBarberBlockCommandHandler : IRequestHandler<DeleteBarberBlockCommand>
{
    private readonly IBarberBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeleteBarberBlockCommandHandler(IBarberBlockRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(DeleteBarberBlockCommand request, CancellationToken cancellationToken)
    {
        var block = await _repo.GetByIdAsync(request.BlockId, cancellationToken);
        if (block is null)
            throw new KeyNotFoundException($"Block '{request.BlockId}' not found.");

        if (block.BarberId != request.BarberId)
            throw new ForbiddenException("You are not authorized to delete this block.");

        await _repo.DeleteAsync(block, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
