using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Reviews;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

public record CreateReviewRequest(Guid AppointmentId, int Rating, string? Comment);

[ApiController]
[Route("api/v1/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Submit a review for a completed appointment (client only).</summary>
    [HttpPost]
    [Authorize(Policy = "RequireClientRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var command = new CreateReviewCommand(request.AppointmentId, clientId, request.Rating, request.Comment);
        var id = await _mediator.Send(command, cancellationToken);
        return Created($"/api/v1/reviews/{id}", new { id });
    }
}
