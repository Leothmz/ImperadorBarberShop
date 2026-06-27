using ImperadorBarberShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ImperadorBarberShop.IntegrationTests;

public class WebAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("imperador_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    // appsettings.json (checked in) has no Jwt/Email section, and there is no
    // appsettings.Testing.json — ASP.NET Core's config chain for the "Testing"
    // environment never loads appsettings.Development.json (gitignored, local-only).
    // Without this, Program.cs throws InvalidOperationException("JWT settings are
    // not configured.") while building WebApplication.CreateBuilder(args) itself.
    //
    // This can't be fixed via ConfigureWebHost/ConfigureAppConfiguration: Program.cs
    // uses the minimal hosting model, and `builder.Configuration.GetSection("Jwt")...`
    // is read directly in top-level statements (Program.cs:56), BEFORE builder.Build()
    // is called (Program.cs:104). WebApplicationFactory's ConfigureWebHost hooks only
    // get applied to the IHostBuilder that wraps an already-running Main() via
    // HostFactoryResolver — by the time they'd run, line 56 has already executed and
    // thrown. Environment variables are read by WebApplication.CreateBuilder's default
    // configuration providers at construction time, so they're visible at line 56.
    private static readonly IReadOnlyDictionary<string, string> TestEnvironmentVariables = new Dictionary<string, string>
    {
        ["Jwt__Secret"] = "integration-test-secret-key-at-least-32-bytes-long!!",
        ["Jwt__Issuer"] = "ImperadorBarberShop",
        ["Jwt__Audience"] = "ImperadorBarberShopFrontend",
        ["Jwt__ExpirationMinutes"] = "15",
        ["Email__SmtpHost"] = "localhost",
        ["Email__SmtpPort"] = "2525",
        ["Email__Username"] = "test",
        ["Email__Password"] = "test",
        ["Email__FromAddress"] = "noreply@test.com",
        ["Email__FromName"] = "Test"
    };

    public async Task InitializeAsync()
    {
        foreach (var (key, value) in TestEnvironmentVariables)
            Environment.SetEnvironmentVariable(key, value);

        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the existing AppDbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Register with the test PostgreSQL container
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Apply migrations
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    public HttpClient CreateAuthenticatedClient(string role, Guid userId, Guid? barberId = null)
    {
        // For integration tests, we generate a real JWT token
        using var scope = Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<ImperadorBarberShop.Application.Interfaces.IJwtService>();

        var user = role == "Barber"
            ? ImperadorBarberShop.Domain.Entities.User.CreateBarber("Test Barber", $"barber-{userId}@test.com", "hash")
            : ImperadorBarberShop.Domain.Entities.User.CreateClient("Test Client", $"client-{userId}@test.com", "hash");

        var token = jwtService.GenerateAccessToken(user, barberId);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
