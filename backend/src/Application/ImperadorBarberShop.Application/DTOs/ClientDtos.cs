namespace ImperadorBarberShop.Application.DTOs;

/// <param name="LastVisitAt">Horário de parede da barbearia, sem fuso.</param>
public record ReinviteCandidateDto(
    Guid ClientId,
    string Name,
    string Phone,
    DateTime LastVisitAt,
    int DaysSinceLastVisit,
    int VisitCount);
