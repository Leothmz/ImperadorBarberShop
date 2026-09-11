using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Phone).IsRequired().HasMaxLength(20);
        builder.Property(c => c.MatchKey).IsRequired().HasMaxLength(10);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.FirstSeenAt).IsRequired();
        builder.Property(c => c.LastVisitAt);
        builder.Property(c => c.VisitCount).IsRequired();
        builder.Property(c => c.LastInviteAt);

        // Uma pessoa, um cliente: segura também dois primeiros agendamentos simultâneos
        builder.HasIndex(c => c.MatchKey).IsUnique();
        builder.HasIndex(c => c.LastVisitAt);
    }
}
