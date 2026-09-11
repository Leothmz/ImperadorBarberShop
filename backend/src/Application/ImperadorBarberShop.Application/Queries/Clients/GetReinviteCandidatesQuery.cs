using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Clients;

/// <summary>
/// Clientes quase perdidos: última visita concluída entre 25 e 30 dias atrás, sem convite nos
/// últimos 10 dias e sem horário marcado pela frente. Os mais próximos de sumir vêm primeiro.
/// </summary>
public record GetReinviteCandidatesQuery : IRequest<List<ReinviteCandidateDto>>;

public class GetReinviteCandidatesQueryHandler : IRequestHandler<GetReinviteCandidatesQuery, List<ReinviteCandidateDto>>
{
    private readonly IClientRepository _clientRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly TimeProvider _clock;

    public GetReinviteCandidatesQueryHandler(
        IClientRepository clientRepository,
        IAppointmentRepository appointmentRepository,
        TimeProvider clock)
    {
        _clientRepository      = clientRepository;
        _appointmentRepository = appointmentRepository;
        _clock                 = clock;
    }

    public async Task<List<ReinviteCandidateDto>> Handle(GetReinviteCandidatesQuery request, CancellationToken cancellationToken)
    {
        // LastVisitAt é horário de parede: compara com o "agora" da barbearia, não com UTC
        var now = _clock.GetLocalNow().DateTime;

        // O banco só corta quem visitou há mais de 30 dias; a regra exata é do Client
        var since = DateOnly.FromDateTime(now)
            .AddDays(-Client.ReinviteWindowEndDays)
            .ToDateTime(TimeOnly.MinValue);
        var inWindow = (await _clientRepository.GetLastVisitedSinceAsync(since, cancellationToken))
            .Where(c => c.IsDueForReinvite(now))
            .ToList();

        var booked = await _appointmentRepository.GetClientIdsWithUpcomingAsync(
            inWindow.Select(c => c.Id).ToList(), now, cancellationToken);

        return inWindow
            .Where(c => !booked.Contains(c.Id))
            .Select(c => new ReinviteCandidateDto(
                c.Id, c.Name, c.Phone, c.LastVisitAt!.Value, c.DaysSinceLastVisit(now)!.Value, c.VisitCount))
            .OrderByDescending(c => c.DaysSinceLastVisit)
            .ThenBy(c => c.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }
}
