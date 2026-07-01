using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ImperadorBarberShop.Application.Commands.Admin;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Application.Queries.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IImageService _imageService;

    public AdminController(IMediator mediator, IImageService imageService)
    {
        _mediator = mediator;
        _imageService = imageService;
    }

    [HttpGet("barbers")]
    public async Task<IActionResult> GetBarbers(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAdminBarbersQuery(), ct));

    [HttpPost("barbers")]
    public async Task<IActionResult> CreateBarber(
        [FromForm] CreateBarberRequest request,
        CancellationToken ct)
    {
        string? photoUrl = null;
        if (request.Photo is not null)
        {
            ValidateImage(request.Photo);
            photoUrl = await _imageService.UploadAsync(
                request.Photo.OpenReadStream(), request.Photo.FileName,
                request.Photo.ContentType, ct);
        }

        var availability = request.Availability ?? new List<AvailabilitySlotInput>();
        var id = await _mediator.Send(
            new CreateBarberByAdminCommand(request.Name, request.Email, request.Password, availability, photoUrl), ct);

        return CreatedAtAction(nameof(GetBarbers), new { id }, new { id });
    }

    [HttpPatch("barbers/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateBarber(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeactivateBarberCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("barbers/{id:guid}/activate")]
    public async Task<IActionResult> ActivateBarber(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ActivateBarberCommand(id), ct);
        return NoContent();
    }

    [HttpPatch("profile/password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await _mediator.Send(new ChangeAdminPasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }

    private static void ValidateImage(IFormFile file)
    {
        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("Image must be smaller than 5 MB.");
        if (file.ContentType is not ("image/jpeg" or "image/png"))
            throw new ArgumentException("Only JPEG and PNG images are accepted.");
    }
}

public record CreateBarberRequest(
    string Name,
    string Email,
    string Password,
    IFormFile? Photo,
    List<AvailabilitySlotInput>? Availability);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
