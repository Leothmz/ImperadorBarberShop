using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetExpensesQuery(DateOnly From, DateOnly To) : IRequest<List<ExpenseDto>>;

public class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, List<ExpenseDto>>
{
    private readonly IExpenseRepository _expenseRepository;
    public GetExpensesQueryHandler(IExpenseRepository expenseRepository) => _expenseRepository = expenseRepository;

    public async Task<List<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        var expenses = await _expenseRepository.GetByDateRangeAsync(request.From, request.To, cancellationToken);
        return expenses.Select(e => new ExpenseDto(e.Id, e.Amount, e.Description, e.Date, e.CreatedAt)).ToList();
    }
}
