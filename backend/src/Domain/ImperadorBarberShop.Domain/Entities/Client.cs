using ImperadorBarberShop.Domain.ValueObjects;

namespace ImperadorBarberShop.Domain.Entities;

/// <summary>
/// Quem já agendou, identificado pelo telefone. Não é conta: o cliente continua anônimo e
/// nunca faz login — este registro só existe para a barbearia reconhecer quem volta.
/// </summary>
public class Client
{
    /// <summary>Janela de recorrência: última visita concluída entre 25 e 30 dias atrás.</summary>
    public const int ReinviteWindowStartDays = 25;
    public const int ReinviteWindowEndDays = 30;

    /// <summary>Quem foi convidado há menos de 10 dias não recebe outro convite.</summary>
    public const int InviteCooldownDays = 10;

    public const string DuplicateMessage =
        "Recebemos outro agendamento deste WhatsApp neste instante. Confira seu WhatsApp antes de tentar de novo.";

    public Guid Id { get; private set; }

    /// <summary>Telefone canônico (<c>+55DDD9XXXXXXXX</c>), para onde vão as mensagens.</summary>
    public string Phone { get; private set; } = string.Empty;

    /// <summary>DDD + 8 últimos dígitos (<see cref="BrazilianPhone.MatchKey"/>). Único.</summary>
    public string MatchKey { get; private set; } = string.Empty;

    /// <summary>Nome digitado no primeiro agendamento; os seguintes nunca o sobrescrevem.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Instante UTC em que o primeiro agendamento foi criado — o relógio de Appointment.CreatedAt.</summary>
    public DateTime FirstSeenAt { get; private set; }

    /// <summary>ScheduledAt (horário de parede da barbearia) do atendimento concluído mais recente.</summary>
    public DateTime? LastVisitAt { get; private set; }

    /// <summary>Atendimentos concluídos. Agendar ou cancelar não conta visita.</summary>
    public int VisitCount { get; private set; }

    /// <summary>Horário de parede da barbearia em que o último convite de retorno foi enfileirado.</summary>
    public DateTime? LastInviteAt { get; private set; }

    // EF Core constructor
    private Client() { }

    public static Client Create(string name, BrazilianPhone phone, DateTime firstSeenAt) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Phone = phone.Canonical,
        MatchKey = phone.MatchKey,
        FirstSeenAt = firstSeenAt,
    };

    /// <param name="visitAt">ScheduledAt do agendamento concluído.</param>
    public void RegisterVisit(DateTime visitAt)
    {
        VisitCount++;
        // Concluir hoje um atendimento antigo não faz a última visita andar para trás
        if (LastVisitAt is null || visitAt > LastVisitAt)
            LastVisitAt = visitAt;
    }

    /// <param name="now">Horário de parede da barbearia.</param>
    public int? DaysSinceLastVisit(DateTime now)
        => LastVisitAt is { } visit ? DaysBetween(visit, now) : null;

    /// <param name="now">Horário de parede da barbearia.</param>
    public bool WasInvitedRecently(DateTime now)
        => LastInviteAt is { } invite && DaysBetween(invite, now) < InviteCooldownDays;

    /// <summary>
    /// Na janela de recorrência e sem convite recente. Quem já tem horário marcado pela frente
    /// também fica de fora, mas isso depende dos agendamentos — quem consulta é que filtra.
    /// </summary>
    /// <param name="now">Horário de parede da barbearia.</param>
    public bool IsDueForReinvite(DateTime now)
        => DaysSinceLastVisit(now) is >= ReinviteWindowStartDays and <= ReinviteWindowEndDays
           && !WasInvitedRecently(now);

    /// <param name="now">Horário de parede da barbearia.</param>
    public void MarkInvited(DateTime now)
    {
        if (WasInvitedRecently(now))
            throw new InvalidOperationException(
                $"Este cliente já foi convidado nos últimos {InviteCooldownDays} dias.");
        LastInviteAt = now;
    }

    // Dias de calendário, como a equipe conta: ontem às 23h foi "há 1 dia"
    private static int DaysBetween(DateTime earlier, DateTime now)
        => DateOnly.FromDateTime(now).DayNumber - DateOnly.FromDateTime(earlier).DayNumber;
}
