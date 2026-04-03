using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user, Guid? barberId = null);
}
