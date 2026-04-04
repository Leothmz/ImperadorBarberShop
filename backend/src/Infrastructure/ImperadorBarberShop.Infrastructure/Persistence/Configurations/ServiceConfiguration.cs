using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    // Deterministic seed IDs — stable across migrations and tests
    public static readonly Guid CorteId         = new("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid FadeId          = new("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid BarbaId         = new("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid SobrancelhaId   = new("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid HidratacaoId    = new("a1000000-0000-0000-0000-000000000005");
    public static readonly Guid PigmentacaoId   = new("a1000000-0000-0000-0000-000000000006");

    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.DurationMinutes).IsRequired();

        builder.Property(s => s.Price)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(s => s.IsActive).IsRequired();

        // Seed data: 6 services — must match the catalog defined in root CLAUDE.md
        builder.HasData(
            Service.CreateWithId(CorteId,       "Corte",             "Haircut",       30, 35.00m),
            Service.CreateWithId(FadeId,         "Fade / Disfarçado", "Fade",          40, 45.00m),
            Service.CreateWithId(BarbaId,        "Barba",             "Beard",         20, 25.00m),
            Service.CreateWithId(SobrancelhaId,  "Sobrancelha",       "Eyebrows",      15, 15.00m),
            Service.CreateWithId(HidratacaoId,   "Hidratação",        "Hydration",     20, 30.00m),
            Service.CreateWithId(PigmentacaoId,  "Pigmentação",       "Pigmentation",  30, 40.00m)
        );
    }
}
