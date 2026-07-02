using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly AppDbContext _context;

    public AppSettingsRepository(AppDbContext context) => _context = context;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => (await _context.AppSettings.FindAsync([key], ct))?.Value;

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await _context.AppSettings.FindAsync([key], ct);
        if (setting is null)
            await _context.AppSettings.AddAsync(AppSettings.Create(key, value), ct);
        else
        {
            setting.SetValue(value);
            _context.AppSettings.Update(setting);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
        => await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);
}
