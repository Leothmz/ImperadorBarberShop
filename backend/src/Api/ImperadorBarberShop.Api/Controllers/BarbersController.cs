using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Commands.Barbers;
using ImperadorBarberShop.Application.Queries.Barbers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

[ApiController]
[Route("api/v1/barbers")]
public class BarbersController : ControllerBase
{
    private readonly IMediator _mediator;

    public BarbersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>List all barbers.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBarbersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get a barber by ID, including availability and average rating.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBarberByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get available time slots for a barber on a given date for the selected services.</summary>
    [HttpGet("{id:guid}/slots")]
    [Authorize(Policy = "RequireClientRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSlots(
        Guid id,
        [FromQuery] DateOnly date,
        [FromQuery] List<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAvailableSlotsQuery(id, date, serviceIds), cancellationToken);
        return Ok(result);
    }

    /// <summary>List reviews for a barber.</summary>
    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviews(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBarberReviewsQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Update the authenticated barber's availability windows.</summary>
    [HttpPut("me/availability")]
    [Authorize(Policy = "RequireBarberRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAvailability(
        [FromBody] List<AvailabilitySlotInput> availability,
        CancellationToken cancellationToken)
    {
        var barberId = Guid.Parse(User.FindFirstValue("barberId")!);
        var command = new UpdateBarberAvailabilityCommand(barberId, availability);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
