using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class BarberConfiguration : IEntityTypeConfiguration<Barber>
{
    public void Configure(EntityTypeBuilder<Barber> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.AverageRating)
            .HasColumnType("decimal(3,2)")
            .IsRequired();

        builder.HasMany(b => b.Availability)
            .WithOne()
            .HasForeignKey(a => a.BarberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Appointments)
            .WithOne(a => a.Barber)
            .HasForeignKey(a => a.BarberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Private backing field for collections
        builder.Navigation(b => b.Availability).HasField("_availability");
        builder.Navigation(b => b.Appointments).HasField("_appointments");
    }
}
