using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Admin;

public class AdminWhatsAppControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AdminWhatsAppControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateAdminClient()
        => _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

    // ── Authorization guards ──────────────────────────────────────────────────

    [Fact]
    public async Task GetWhatsAppStatus_Unauthenticated_Returns401()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/whatsapp/status");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /admin/whatsapp/status ────────────────────────────────────────────

    [Fact]
    public async Task GetWhatsAppStatus_AsAdmin_Returns200WithStatusField()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/v1/admin/whatsapp/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().NotBeNullOrEmpty();
    }

    // ── GET /admin/whatsapp/qr ────────────────────────────────────────────────

    [Fact]
    public async Task GetWhatsAppQr_AsAdmin_DoesNotCrashApp()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/v1/admin/whatsapp/qr");

        // Evolution API is unavailable in tests; we accept 200 or any non-5xx response
        ((int)response.StatusCode).Should().BeLessThan(500);
    }

    // ── POST /admin/whatsapp/disconnect ───────────────────────────────────────

    [Fact]
    public async Task DisconnectWhatsApp_AsAdmin_Returns200Or204()
    {
        var client = CreateAdminClient();
        var response = await client.PostAsync("/api/v1/admin/whatsapp/disconnect", null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    // ── GET /admin/notifications/settings ────────────────────────────────────

    [Fact]
    public async Task GetNotificationSettings_AsAdmin_Returns200WithExpectedFields()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/v1/admin/notifications/settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.TryGetProperty("channels", out _).Should().BeTrue();
        body.TryGetProperty("reminderMinutesBefore", out _).Should().BeTrue();
        body.TryGetProperty("notificationPhone", out _).Should().BeTrue();
    }

    // ── PUT /admin/notifications/settings ────────────────────────────────────

    [Fact]
    public async Task UpdateNotificationSettings_AsAdmin_Returns200Or204()
    {
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync("/api/v1/admin/notifications/settings",
            new { channels = new[] { "email" }, reminderMinutesBefore = 60, notificationPhone = (string?)null });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }
}
