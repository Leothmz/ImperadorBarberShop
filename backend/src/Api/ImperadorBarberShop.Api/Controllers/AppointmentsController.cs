using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Commands.Reviews;
using ImperadorBarberShop.Application.Queries.Appointments;
using ImperadorBarberShop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImperadorBarberShop.Api.Controllers;

public record CreateAppointmentRequest(
    string ClientName,
    string ClientPhone,
    Guid BarberId,
    DateTime ScheduledAt,
    List<Guid> ServiceIds,
    string? Notes);

public record CreateReviewByTokenRequest(int Rating, string? Comment);

[ApiController]
[Route("api/v1/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AppointmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a new appointment. Public — no account required. Auto-confirmed.</summary>
    [HttpPost]
    [EnableRateLimiting("appointment-creation")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAppointmentCommand(
            request.ClientName, request.ClientPhone, request.BarberId,
            request.ScheduledAt, request.ServiceIds, request.Notes);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetByToken), new { token = result.AccessToken }, result);
    }

    /// <summary>Get an appointment's public status by its management token.</summary>
    [HttpGet("manage/{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByToken(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAppointmentByTokenQuery(token), cancellationToken);
        return Ok(result);
    }

    /// <summary>Cancel an appointment via its management token (must be >2h before scheduled time).</summary>
    [HttpPost("manage/{token}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelByToken(string token, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CancelAppointmentByTokenCommand(token), cancellationToken);
        return NoContent();
    }

    /// <summary>Submit a review via the management token (only once the appointment is Completed).</summary>
    [HttpPost("manage/{token}/review")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReviewByToken(
        string token,
        [FromBody] CreateReviewByTokenRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(
            new CreateReviewByTokenCommand(token, request.Rating, request.Comment), cancellationToken);
        return Created($"/api/v1/reviews/{id}", new { id });
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

    /// <summary>Cancel a confirmed appointment (barber-initiated, e.g. emergencies).</summary>
    [HttpPatch("{id:guid}/cancel-by-barber")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelByBarber(Guid id, CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new CancelAppointmentByBarberCommand(id, barberId), cancellationToken);
        return NoContent();
    }

    /// <summary>Mark an accepted appointment as completed (barber only). Optionally accepts payment method.</summary>
    [HttpPatch("{id:guid}/complete")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        CompleteAppointmentRequest? request,
        CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new CompleteAppointmentCommand(id, barberId, request?.PaymentMethod), cancellationToken);
        return NoContent();
    }

    /// <summary>Update the payment method for a completed appointment (barber only).</summary>
    [HttpPatch("{id:guid}/payment")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePayment(
        Guid id,
        [FromBody] UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        await _mediator.Send(new UpdatePaymentMethodCommand(id, request.PaymentMethod, barberId), cancellationToken);
        return NoContent();
    }
}

public record CompleteAppointmentRequest(PaymentMethod? PaymentMethod);
public record UpdatePaymentMethodRequest(PaymentMethod PaymentMethod);
