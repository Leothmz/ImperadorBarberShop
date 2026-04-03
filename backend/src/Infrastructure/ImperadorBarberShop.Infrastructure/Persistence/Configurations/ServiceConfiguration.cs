using ImperadorBarberShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ImperadorBarberShop.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    // Deterministic seed IDs
    public static readonly Guid HaircutId = new("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid BeardId = new("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid HaircutBeardId = new("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid KidsHaircutId = new("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid EyebrowId = new("a1000000-0000-0000-0000-000000000005");
    public static readonly Guid StraightRazorId = new("a1000000-0000-0000-0000-000000000006");

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

        // Seed data: 6 services
        builder.HasData(
            Service.CreateWithId(HaircutId, "Corte de Cabelo", "Corte moderno e estiloso", 30, 35.00m),
            Service.CreateWithId(BeardId, "Barba", "Aparar e modelar a barba", 20, 25.00m),
            Service.CreateWithId(HaircutBeardId, "Corte + Barba", "Corte de cabelo e barba completo", 50, 55.00m),
            Service.CreateWithId(KidsHaircutId, "Corte Infantil", "Corte para crianças até 12 anos", 25, 28.00m),
            Service.CreateWithId(EyebrowId, "Sobrancelha", "Modelagem de sobrancelha masculina", 15, 18.00m),
            Service.CreateWithId(StraightRazorId, "Navalhada", "Barba completa com navalha quente", 30, 40.00m)
        );
    }
}
