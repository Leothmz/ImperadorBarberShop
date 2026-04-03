namespace ImperadorBarberShop.Domain.Entities;

public class AppointmentService
{
    public Guid AppointmentId { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service Service { get; private set; } = null!;

    // EF Core constructor
    private AppointmentService() { }

    public static AppointmentService Create(Guid appointmentId, Guid serviceId)
    {
        return new AppointmentService
        {
            AppointmentId = appointmentId,
            ServiceId = serviceId
        };
    }
}
