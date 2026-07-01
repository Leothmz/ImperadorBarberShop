using ImperadorBarberShop.Application.Commands.Services;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Application.Queries.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
public class ServicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IImageService _imageService;

    public ServicesController(IMediator mediator, IImageService imageService)
    {
        _mediator = mediator;
        _imageService = imageService;
    }

    /// <summary>Get all active services.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetServicesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> CreateService([FromForm] CreateServiceRequest request, CancellationToken ct)
    {
        string? photoUrl = null;
        if (request.Photo is not null)
        {
            ValidateImage(request.Photo);
            photoUrl = await _imageService.UploadAsync(
                request.Photo.OpenReadStream(), request.Photo.FileName, request.Photo.ContentType, ct);
        }
        var id = await _mediator.Send(
            new CreateServiceCommand(request.Name, request.Description, request.Price, request.DurationMinutes, photoUrl), ct);
        return CreatedAtAction(nameof(GetServices), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> UpdateService(Guid id, [FromForm] UpdateServiceRequest request, CancellationToken ct)
    {
        string? photoUrl = null;
        if (request.Photo is not null)
        {
            ValidateImage(request.Photo);
            photoUrl = await _imageService.UploadAsync(
                request.Photo.OpenReadStream(), request.Photo.FileName, request.Photo.ContentType, ct);
        }
        await _mediator.Send(
            new UpdateServiceCommand(id, request.Name, request.Description, request.Price, request.DurationMinutes, photoUrl), ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> DeactivateService(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeactivateServiceCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> ActivateService(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ActivateServiceCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/addons/{addonId:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AddAddon(Guid id, Guid addonId, CancellationToken ct)
    {
        await _mediator.Send(new AddServiceAddonCommand(id, addonId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/addons/{addonId:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> RemoveAddon(Guid id, Guid addonId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveServiceAddonCommand(id, addonId), ct);
        return NoContent();
    }

    private static void ValidateImage(IFormFile file)
    {
        if (file.Length > 5 * 1024 * 1024) throw new ArgumentException("Image must be smaller than 5 MB.");
        if (file.ContentType is not ("image/jpeg" or "image/png")) throw new ArgumentException("Only JPEG and PNG images are accepted.");
    }
}

public record CreateServiceRequest(string Name, string Description, decimal Price, int DurationMinutes, IFormFile? Photo);
public record UpdateServiceRequest(string Name, string Description, decimal Price, int DurationMinutes, IFormFile? Photo);
