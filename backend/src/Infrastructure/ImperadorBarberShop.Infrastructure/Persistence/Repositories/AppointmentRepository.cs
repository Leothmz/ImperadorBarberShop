using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _context;

    public AppointmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<List<Appointment>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<List<Appointment>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default)
        => await _context.Appointments
            .Include(a => a.Client)
            .Include(a => a.Barber).ThenInclude(b => b.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .Where(a => a.BarberId == barberId)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);

    public async Task<List<Appointment>> GetActiveByBarberIdAndDateAsync(
        Guid barberId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.Appointments
            .Where(a => a.BarberId == barberId
                && a.ScheduledAt >= dayStart
                && a.ScheduledAt <= dayEnd
                && a.Status != AppointmentStatus.Rejected
                && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
        => await _context.Appointments.AddAsync(appointment, cancellationToken);

    public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _context.Appointments.Update(appointment);
        return Task.CompletedTask;
    }
}
