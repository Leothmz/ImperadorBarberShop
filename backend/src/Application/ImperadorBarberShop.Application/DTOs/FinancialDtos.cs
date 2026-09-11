namespace ImperadorBarberShop.Application.DTOs;

public record FinancialSummaryDto(
    decimal TotalRevenue,
    int TotalAppointments,
    decimal AverageTicket,
    DateOnly From,
    DateOnly To,
    decimal TotalExpenses,
    decimal NetRevenue);

public record FinancialTimelineItemDto(string Period, decimal Revenue, int Appointments);

public record FinancialByBarberItemDto(Guid BarberId, string BarberName, int Appointments, decimal Revenue);

public record FinancialByServiceItemDto(Guid ServiceId, string ServiceName, int Count, decimal Revenue)
{
    /// <summary>
    /// Linha sintética dos atendimentos de plano: cada um conta uma vez, pelo valor cobrado
    /// (0 na recorrência), em vez de repartir o valor pelos serviços que recebeu.
    /// </summary>
    public const string PlanServiceName = "Plano";

    public static readonly Guid PlanServiceId = Guid.Empty;
}

public record ExpenseDto(Guid Id, decimal Amount, string Description, DateOnly Date, DateTime CreatedAt);
