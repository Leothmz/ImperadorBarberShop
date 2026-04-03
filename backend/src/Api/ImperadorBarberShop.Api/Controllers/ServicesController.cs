using ImperadorBarberShop.Application.Queries.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
public class ServicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ServicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Get all active services.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetServicesQuery(), cancellationToken);
        return Ok(result);
    }
}
