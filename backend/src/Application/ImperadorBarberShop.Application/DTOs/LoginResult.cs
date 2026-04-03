namespace ImperadorBarberShop.Application.DTOs;

public record LoginResult(
    string AccessToken,
    string RefreshToken,
    string Role,
    Guid UserId,
    Guid? BarberId);
