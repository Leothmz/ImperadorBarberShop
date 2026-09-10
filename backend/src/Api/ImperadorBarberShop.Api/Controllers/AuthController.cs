using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ImperadorBarberShop.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Cookie HttpOnly com o refresh token. O JavaScript da página nunca lê o token, e o
    /// middleware do Next usa a presença deste cookie — que só a API emite — para
    /// liberar /admin e /barber, no lugar do antigo cookie de papel forjável.
    /// </summary>
    public const string RefreshCookieName = "imperador_refresh_token";

    private static readonly TimeSpan RefreshCookieLifetime = TimeSpan.FromDays(7); // = validade do refresh token

    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    /// <summary>Authenticate: returns the access token and sets the refresh token as an HttpOnly cookie.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return SessionResponse(result);
    }

    /// <summary>Exchange the refresh-token cookie for a new access token (and a rotated cookie).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        try
        {
            var result = await _mediator.Send(new RefreshTokenCommand(request.UserId, refreshToken), cancellationToken);
            return SessionResponse(result);
        }
        catch (UnauthorizedAccessException)
        {
            // Cookie morto não pode continuar abrindo a porta do middleware do Next
            Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
            throw;
        }
    }

    /// <summary>End the session: clears the refresh-token cookie, which page JavaScript cannot delete.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
        return NoContent();
    }

    private IActionResult SessionResponse(LoginResult result)
    {
        var options = RefreshCookieOptions();
        options.Expires = DateTimeOffset.UtcNow.Add(RefreshCookieLifetime);
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, options);

        // O refresh token fica só no cookie: devolvê-lo no corpo o exporia ao JavaScript
        return Ok(new SessionResult(result.AccessToken, result.Role, result.UserId, result.BarberId));
    }

    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        // Em dev o site roda em http (localhost, IP da rede local no celular), onde um
        // cookie Secure seria descartado. Fora de dev vale sempre, mesmo que o TLS termine
        // num proxy antes da API.
        Secure = Request.IsHttps || !_environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        // Path "/": o middleware do Next precisa enxergar o cookie em /admin e /barber
        Path = "/",
        IsEssential = true,
    };

    public record RefreshRequest(Guid UserId);

    public record SessionResult(string AccessToken, string Role, Guid UserId, Guid? BarberId);
}
