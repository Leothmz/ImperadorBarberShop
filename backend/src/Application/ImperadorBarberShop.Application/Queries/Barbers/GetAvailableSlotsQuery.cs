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
    private readonly IBarberBlockRepository _blockRepository;

    public GetAvailableSlotsQueryHandler(
        IBarberAvailabilityRepository availabilityRepository,
        IAppointmentRepository appointmentRepository,
        IServiceRepository serviceRepository,
        IBarberBlockRepository blockRepository)
    {
        _availabilityRepository = availabilityRepository;
        _appointmentRepository = appointmentRepository;
        _serviceRepository = serviceRepository;
        _blockRepository = blockRepository;
    }

    public async Task<List<TimeOnly>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
    {
        var dayOfWeek = request.Date.DayOfWeek;
        var availability = await _availabilityRepository.GetByBarberIdAndDayAsync(
            request.BarberId, dayOfWeek, cancellationToken);

        if (availability is null)
            return new List<TimeOnly>();

        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count == 0)
            return new List<TimeOnly>();

        var totalDuration = services.Sum(s => s.DurationMinutes);

        var activeAppointments = await _appointmentRepository.GetActiveByBarberIdAndDateAsync(
            request.BarberId, request.Date, cancellationToken);

        var blocks = await _blockRepository.GetActiveOnDateAsync(
            request.BarberId, request.Date, cancellationToken);

        // Build all occupied intervals (appointments + blocks)
        var occupiedBlocks = activeAppointments
            .Select(a => (Start: a.ScheduledAt, End: a.ScheduledAt.AddMinutes(a.TotalDurationMinutes)))
            .Concat(blocks.Select(b => (
                Start: request.Date.ToDateTime(TimeOnly.FromDateTime(b.StartsAt), DateTimeKind.Utc),
                End: request.Date.ToDateTime(TimeOnly.FromDateTime(b.EndsAt), DateTimeKind.Utc))))
            .ToList();

        var slots = new List<TimeOnly>();
        var current = availability.StartTime;
        var windowEnd = availability.EndTime.AddMinutes(-totalDuration);

        while (current <= windowEnd)
        {
            var slotStart = request.Date.ToDateTime(current);
            var slotEnd = slotStart.AddMinutes(totalDuration);

            var hasOverlap = occupiedBlocks.Any(block =>
                slotStart < block.End && slotEnd > block.Start);

            if (!hasOverlap && slotStart > DateTime.UtcNow)
                slots.Add(current);

            current = current.AddMinutes(15);
        }

        return slots;
    }
}
