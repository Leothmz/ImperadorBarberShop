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
        builder.Property(a => a.ReminderSentAt);
        builder.Property(a => a.PaymentMethod);
        builder.Property(a => a.PaidAt);
        builder.Property(a => a.PlanKind);
        builder.Property(a => a.PlanTender);
        builder.Property(a => a.ChargedAmount);

        // Calculados a partir das colunas acima e dos serviços, nunca gravados
        builder.Ignore(a => a.EffectiveAmount);
        builder.Ignore(a => a.IsPlan);

        builder.HasIndex(a => new { a.BarberId, a.ScheduledAt }).IsUnique();
        builder.HasIndex(a => a.AccessToken).IsUnique();

        // Agendamento é registro financeiro: apagar o cliente nunca apaga o histórico
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(a => a.AppointmentServices)
            .WithOne()
            .HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Private backing field
        builder.Navigation(a => a.AppointmentServices).HasField("_appointmentServices");
    }
}
