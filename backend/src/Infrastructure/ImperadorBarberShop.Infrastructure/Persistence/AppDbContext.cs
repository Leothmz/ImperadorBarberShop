using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence;

public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Barber> Barbers => Set<Barber>();
    public DbSet<BarberAvailability> BarberAvailabilities => Set<BarberAvailability>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ServiceAddon> ServiceAddons => Set<ServiceAddon>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<BarberBlock> BarberBlocks => Set<BarberBlock>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.AddInterceptors(PhoneSqlFunctions.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        // Dois primeiros agendamentos do mesmo WhatsApp ao mesmo tempo: os dois não acham o
        // cliente e tentam criá-lo. O índice único de MatchKey barra o segundo.
        catch (DbUpdateException ex) when (
            ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintFailed } sqlite
            && sqlite.Message.Contains("Clients.MatchKey", StringComparison.Ordinal))
        {
            throw new ConflictException(Client.DuplicateMessage);
        }
        // O índice único (BarberId, ScheduledAt) segura duas reservas simultâneas do mesmo
        // horário, que passam juntas pela checagem de sobreposição. Quem perde a corrida
        // recebe o mesmo 409 da checagem, não um 500.
        catch (DbUpdateException ex) when (
            ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintFailed }
            && ex.Entries.Any(e => e.Entity is Appointment && e.State == EntityState.Added))
        {
            throw new ConflictException(Appointment.SlotTakenMessage);
        }
    }

    private const int SqliteUniqueConstraintFailed = 2067; // SQLITE_CONSTRAINT_UNIQUE
}
