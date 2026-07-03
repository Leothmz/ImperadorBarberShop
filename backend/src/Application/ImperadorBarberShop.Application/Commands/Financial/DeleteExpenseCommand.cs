using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Financial;

public record DeleteExpenseCommand(Guid Id) : IRequest;

public class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand>
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteExpenseCommandHandler(IExpenseRepository expenseRepository, IUnitOfWork unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (expense is null)
            throw new KeyNotFoundException($"Expense '{request.Id}' not found.");
        await _expenseRepository.DeleteAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
