using ImperadorBarberShop.Domain.Interfaces;
using MediatR;
using System.Text;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record ExportFinancialCsvQuery(DateOnly From, DateOnly To) : IRequest<string>;

public class ExportFinancialCsvQueryHandler : IRequestHandler<ExportFinancialCsvQuery, string>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public ExportFinancialCsvQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<string> Handle(ExportFinancialCsvQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Data,Barbeiro,Cliente,Telefone,Serviço,Preço,AgendamentoId");

        foreach (var a in appointments)
        {
            var date = a.ScheduledAt.ToString("yyyy-MM-dd");
            var barber = EscapeCsv(a.Barber.User.Name);
            var client = EscapeCsv(a.ClientName);
            var phone = MaskPhone(a.ClientPhone);

            foreach (var aps in a.AppointmentServices)
            {
                sb.AppendLine(
                    $"{date},{barber},{client},{phone},{EscapeCsv(aps.Service.Name)},{aps.Service.Price:F2},{a.Id}");
            }
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    // Masks all but last 4 digits: +5511999990000 → +55119999****
    private static string MaskPhone(string phone)
        => phone.Length <= 4 ? phone : phone[..^4] + "****";
}
