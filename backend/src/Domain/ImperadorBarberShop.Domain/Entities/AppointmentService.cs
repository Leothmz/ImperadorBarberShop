namespace ImperadorBarberShop.Domain.Entities;

public class AppointmentService
{
    public Guid AppointmentId { get; private set; }
    public Guid ServiceId { get; private set; }

    /// <summary>
    /// Preço do serviço no momento do agendamento. O financeiro soma este valor, não o
    /// preço atual do catálogo — senão um reajuste reescreveria o faturamento passado.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    public Service Service { get; private set; } = null!;

    // EF Core constructor
    private AppointmentService() { }

    public static AppointmentService Create(Guid appointmentId, Service service)
    {
        return new AppointmentService
        {
            AppointmentId = appointmentId,
            ServiceId = service.Id,
            UnitPrice = service.Price
        };
    }
}
