using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class BarberAvailabilityConfiguration : IEntityTypeConfiguration<BarberAvailability>
{
    public void Configure(EntityTypeBuilder<BarberAvailability> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DayOfWeek).IsRequired();
        builder.Property(a => a.StartTime).IsRequired();
        builder.Property(a => a.EndTime).IsRequired();

        builder.HasIndex(a => new { a.BarberId, a.DayOfWeek })
            .IsUnique();
    }
}
