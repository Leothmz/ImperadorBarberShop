using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ClientName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ClientPhone).IsRequired().HasMaxLength(20);
        builder.Property(a => a.AccessToken).IsRequired().HasMaxLength(64);
        builder.Property(a => a.ScheduledAt).IsRequired();
        builder.Property(a => a.TotalDurationMinutes).IsRequired();
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(500);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasIndex(a => new { a.BarberId, a.ScheduledAt }).IsUnique();
        builder.HasIndex(a => a.AccessToken).IsUnique();

        builder.HasMany(a => a.AppointmentServices)
            .WithOne()
            .HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Private backing field
        builder.Navigation(a => a.AppointmentServices).HasField("_appointmentServices");
    }
}
