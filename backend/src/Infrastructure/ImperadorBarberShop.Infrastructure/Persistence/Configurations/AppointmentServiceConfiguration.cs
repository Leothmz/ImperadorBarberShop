using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppointmentServiceEntity = ImperadorBarberShop.Domain.Entities.AppointmentService;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class AppointmentServiceConfiguration : IEntityTypeConfiguration<AppointmentServiceEntity>
{
    public void Configure(EntityTypeBuilder<AppointmentServiceEntity> builder)
    {
        builder.HasKey(a => new { a.AppointmentId, a.ServiceId });

        builder.HasOne(a => a.Service)
            .WithMany()
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
