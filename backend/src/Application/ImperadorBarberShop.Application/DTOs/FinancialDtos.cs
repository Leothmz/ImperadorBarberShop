namespace ImperadorBarberShop.Application.DTOs;

public record FinancialSummaryDto(decimal TotalRevenue, int TotalAppointments, decimal AverageTicket, DateOnly From, DateOnly To);

public record FinancialByBarberItemDto(Guid BarberId, string BarberName, int Appointments, decimal Revenue);

public record FinancialByServiceItemDto(Guid ServiceId, string ServiceName, int Count, decimal Revenue);

public record ExpenseDto(Guid Id, decimal Amount, string Description, DateOnly Date, DateTime CreatedAt);
