using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class ServiceAddonConfiguration : IEntityTypeConfiguration<ServiceAddon>
{
    public void Configure(EntityTypeBuilder<ServiceAddon> builder)
    {
        builder.HasKey(a => new { a.ParentServiceId, a.AddonServiceId });

        builder.HasOne(a => a.AddonService)
            .WithMany()
            .HasForeignKey(a => a.AddonServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(a => a.ParentServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ServiceAddons_NoCycles",
            "\"ParentServiceId\" <> \"AddonServiceId\""));
    }
}
