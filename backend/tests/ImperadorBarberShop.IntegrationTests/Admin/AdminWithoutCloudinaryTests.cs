using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ImperadorBarberShop.IntegrationTests.Admin;

/// <summary>
/// Without Cloudinary configured, only photo upload may fail — and with a message the
/// admin can act on. AdminController takes IImageService in its constructor, and the
/// Cloudinary client used to be built there, so every /admin endpoint returned 500.
/// </summary>
public class AdminWithoutCloudinaryTests : IClassFixture<AdminWithoutCloudinaryTests.NoCloudinaryFixture>
{
    private readonly HttpClient _admin;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AdminWithoutCloudinaryTests(NoCloudinaryFixture fixture)
    {
        _admin = fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
    }

    [Theory]
    [InlineData("/api/v1/admin/barbers")]
    [InlineData("/api/v1/admin/services")]
    [InlineData("/api/v1/admin/financial/summary?from=2026-07-01&to=2026-07-31")]
    [InlineData("/api/v1/admin/whatsapp/status")]
    [InlineData("/api/v1/admin/notifications/settings")]
    public async Task AdminEndpoints_WorkWithCloudinaryUnset(string url)
    {
        var response = await _admin.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateBarber_WithoutPhoto_StillWorks()
    {
        using var form = BarberForm();

        var response = await _admin.PostAsync("/api/v1/admin/barbers", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBarber_WithPhoto_ReportsThatUploadIsDisabled()
    {
        using var form = BarberForm();
        var photo = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(photo, "photo", "foto.png");

        var response = await _admin.PostAsync("/api/v1/admin/barbers", form);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("detail").GetString().Should().Be(CloudinaryImageService.NotConfiguredMessage);
    }

    private static MultipartFormDataContent BarberForm() => new()
    {
        { new StringContent("Barbeiro Sem Foto"), "name" },
        { new StringContent($"sem-foto-{Guid.NewGuid()}@test.com"), "email" },
        { new StringContent("Password123!"), "password" },
    };

    /// <summary>The real Cloudinary service, with the blank settings of an unconfigured deploy.</summary>
    public class NoCloudinaryFixture : WebAppFixture
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IImageService>();
                services.AddScoped<IImageService, CloudinaryImageService>();
                services.RemoveAll<IOptions<CloudinarySettings>>();
                services.AddSingleton(Options.Create(new CloudinarySettings()));
            });
        }
    }
}
