using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Repositories;
using ImperadorBarberShop.Infrastructure.Services;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ImperadorBarberShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBarberRepository, BarberRepository>();
        services.AddScoped<IBarberAvailabilityRepository, BarberAvailabilityRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IServiceAddonRepository, ServiceAddonRepository>();
        services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
        services.AddHttpClient();
        services.AddScoped<IWhatsAppService>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("evolution");
            var repo = sp.GetRequiredService<IAppSettingsRepository>();
            return new EvolutionApiWhatsAppService(http, repo);
        });

        // Services
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IImageService, CloudinaryImageService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<AdminSeedService>();
        services.AddHostedService<ReminderBackgroundService>();

        return services;
    }
}
