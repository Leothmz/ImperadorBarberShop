using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ImperadorBarberShop.Application.Commands.Admin;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Application.Queries.Admin;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Application.Queries.Services;
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
            var validationError = GetImageValidationError(request.Photo);
            if (validationError is not null) return BadRequest(new { error = validationError });
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

    [HttpGet("financial/summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFinancialSummaryQuery(from, to), ct));

    [HttpGet("financial/by-barber")]
    public async Task<IActionResult> GetByBarber(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFinancialByBarberQuery(from, to), ct));

    [HttpGet("services")]
    public async Task<IActionResult> GetAllServices(CancellationToken ct)
        => Ok(await _mediator.Send(new GetServicesQuery(IncludeInactive: true), ct));

    [HttpGet("financial/by-service")]
    public async Task<IActionResult> GetByService(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFinancialByServiceQuery(from, to), ct));

    [HttpGet("financial/export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        var csv = await _mediator.Send(new ExportFinancialCsvQuery(from, to), ct);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"relatorio-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.csv");
    }

    // WhatsApp
    [HttpGet("whatsapp/status")]
    public async Task<IActionResult> GetWhatsAppStatus(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWhatsAppStatusQuery(), ct));

    [HttpGet("whatsapp/qr")]
    public async Task<IActionResult> GetWhatsAppQr(CancellationToken ct)
        => Ok(await _mediator.Send(new GetWhatsAppQrQuery(), ct));

    [HttpPost("whatsapp/disconnect")]
    public async Task<IActionResult> DisconnectWhatsApp(CancellationToken ct)
    {
        await _mediator.Send(new DisconnectWhatsAppCommand(), ct);
        return NoContent();
    }

    // Notification settings
    [HttpGet("notifications/settings")]
    public async Task<IActionResult> GetNotificationSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetNotificationSettingsQuery(), ct));

    [HttpPut("notifications/settings")]
    public async Task<IActionResult> UpdateNotificationSettings(
        [FromBody] UpdateNotificationSettingsRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateNotificationSettingsCommand(
            request.Channels, request.ReminderMinutesBefore, request.NotificationPhone), ct);
        return NoContent();
    }

    private static string? GetImageValidationError(IFormFile file)
    {
        if (file.Length > 5 * 1024 * 1024) return "Image must be smaller than 5 MB.";
        if (file.ContentType is not ("image/jpeg" or "image/png")) return "Only JPEG and PNG images are accepted.";
        return null;
    }
}

public record CreateBarberRequest(
    string Name,
    string Email,
    string Password,
    IFormFile? Photo,
    List<AvailabilitySlotInput>? Availability);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UpdateNotificationSettingsRequest(
    List<string> Channels,
    int ReminderMinutesBefore,
    string? NotificationPhone);
