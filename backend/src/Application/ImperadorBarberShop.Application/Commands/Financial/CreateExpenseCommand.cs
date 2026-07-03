using FluentValidation;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Financial;

public record CreateExpenseCommand(decimal Amount, string Description, DateOnly Date, Guid CreatedByUserId) : IRequest<Guid>;

public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Valor deve ser maior que zero.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreatedByUserId).NotEmpty();
    }
}

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Guid>
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateExpenseCommandHandler(IExpenseRepository expenseRepository, IUnitOfWork unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = Expense.Create(request.Amount, request.Description, request.Date, request.CreatedByUserId);
        await _expenseRepository.AddAsync(expense, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }
}
