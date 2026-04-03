using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Queries.Appointments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

public record CreateAppointmentRequest(
    Guid BarberId,
    DateTime ScheduledAt,
    List<Guid> ServiceIds,
    string? Notes);

[ApiController]
[Route("api/v1/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AppointmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a new appointment (client only).</summary>
    [HttpPost]
    [Authorize(Policy = "RequireClientRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var command = new CreateAppointmentCommand(
            clientId, request.BarberId, request.ScheduledAt, request.ServiceIds, request.Notes);
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetMine), null, new { id });
    }

    /// <summary>List authenticated client's appointments.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = "RequireClientRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var clientId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var result = await _mediator.Send(new GetClientAppointmentsQuery(clientId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Cancel an appointment (client only, must be >2h before scheduled time).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireClientRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var clientId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await _mediator.Send(new CancelAppointmentCommand(id, clientId), cancellationToken);
        return NoContent();
    }

    /// <summary>List all appointments for the authenticated barber.</summary>
    [HttpGet("barber")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBarberAppointments(CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        var result = await _mediator.Send(new GetBarberAppointmentsQuery(barberId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Accept a pending appointment (barber only).</summary>
    [HttpPatch("{id:guid}/accept")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new AcceptAppointmentCommand(id, barberId), cancellationToken);
        return NoContent();
    }

    /// <summary>Reject a pending appointment (barber only).</summary>
    [HttpPatch("{id:guid}/reject")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new RejectAppointmentCommand(id, barberId), cancellationToken);
        return NoContent();
    }

    /// <summary>Mark an accepted appointment as completed (barber only).</summary>
    [HttpPatch("{id:guid}/complete")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new CompleteAppointmentCommand(id, barberId), cancellationToken);
        return NoContent();
    }
}
