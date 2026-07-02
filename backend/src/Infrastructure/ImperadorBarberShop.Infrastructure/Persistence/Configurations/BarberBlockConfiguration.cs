using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class BarberBlockConfiguration : IEntityTypeConfiguration<BarberBlock>
{
    public void Configure(EntityTypeBuilder<BarberBlock> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BarberId).IsRequired();
        builder.Property(b => b.StartsAt).IsRequired();
        builder.Property(b => b.EndsAt).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(200);
        builder.Property(b => b.IsRecurring).IsRequired();
        builder.Property(b => b.RecurrenceDays);
        builder.Property(b => b.RecurrenceEndsAt);
        builder.Property(b => b.CreatedAt).IsRequired();

        builder.HasIndex(b => b.BarberId);
    }
}
