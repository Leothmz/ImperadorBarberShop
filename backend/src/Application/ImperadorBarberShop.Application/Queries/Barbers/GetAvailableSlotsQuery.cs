using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Barbers;

public record GetAvailableSlotsQuery(
    Guid BarberId,
    DateOnly Date,
    List<Guid> ServiceIds) : IRequest<List<TimeOnly>>;

public class GetAvailableSlotsQueryHandler : IRequestHandler<GetAvailableSlotsQuery, List<TimeOnly>>
{
    private readonly IBarberAvailabilityRepository _availabilityRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IServiceRepository _serviceRepository;

    public GetAvailableSlotsQueryHandler(
        IBarberAvailabilityRepository availabilityRepository,
        IAppointmentRepository appointmentRepository,
        IServiceRepository serviceRepository)
    {
        _availabilityRepository = availabilityRepository;
        _appointmentRepository = appointmentRepository;
        _serviceRepository = serviceRepository;
    }

    public async Task<List<TimeOnly>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        // Step 1: Load barber availability for the requested day
        var dayOfWeek = request.Date.DayOfWeek;
        var availability = await _availabilityRepository.GetByBarberIdAndDayAsync(
            request.BarberId, dayOfWeek, cancellationToken);

        // Step 2: If no availability defined for this day, return empty
        if (availability is null)
            return new List<TimeOnly>();

        // Step 3: Calculate total duration of requested services
        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count == 0)
            return new List<TimeOnly>();

        var totalDuration = services.Sum(s => s.DurationMinutes);

        // Step 4: Load non-cancelled/rejected appointments for barber on that date
        var activeAppointments = await _appointmentRepository.GetActiveByBarberIdAndDateAsync(
            request.BarberId, request.Date, cancellationToken);

        // Step 5: Build occupied blocks
        var occupiedBlocks = activeAppointments
            .Select(a => (Start: a.ScheduledAt, End: a.ScheduledAt.AddMinutes(a.TotalDurationMinutes)))
            .ToList();

        // Step 6: Walk availability in 15-minute increments and collect free slots
        var slots = new List<TimeOnly>();
        var current = availability.StartTime;
        var windowEnd = availability.EndTime.AddMinutes(-totalDuration);

        while (current <= windowEnd)
        {
            var slotStart = request.Date.ToDateTime(current);
            var slotEnd = slotStart.AddMinutes(totalDuration);

            var hasOverlap = occupiedBlocks.Any(block =>
                slotStart < block.End && slotEnd > block.Start);

            if (!hasOverlap)
                slots.Add(current);

            current = current.AddMinutes(15);
        }

        return slots;
    }
}
