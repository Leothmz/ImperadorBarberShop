using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Admin;

public class AdminBarbersControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AdminBarbersControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateAdminClient()
        => _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

    // ── Authorization guards ──────────────────────────────────────────────────

    [Fact]
    public async Task GetBarbers_Unauthenticated_Returns401()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/barbers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBarbers_AsBarber_Returns403()
    {
        var client = _fixture.CreateAuthenticatedClient("Barber", Guid.NewGuid(), Guid.NewGuid());
        var response = await client.GetAsync("/api/v1/admin/barbers");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /admin/barbers ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBarbers_AsAdmin_Returns200WithList()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/v1/admin/barbers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── POST /admin/barbers ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateBarber_AsAdmin_Returns201()
    {
        var client = CreateAdminClient();
        var email = $"newbarber-{Guid.NewGuid()}@test.com";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Novo Barbeiro"), "Name");
        form.Add(new StringContent(email), "Email");
        form.Add(new StringContent("Password123!"), "Password");

        var response = await client.PostAsync("/api/v1/admin/barbers", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateBarber_DuplicateEmail_Returns422()
    {
        var client = CreateAdminClient();
        var email = $"dup-barber-{Guid.NewGuid()}@test.com";

        // First creation
        using var form1 = new MultipartFormDataContent();
        form1.Add(new StringContent("Barber One"), "Name");
        form1.Add(new StringContent(email), "Email");
        form1.Add(new StringContent("Password123!"), "Password");
        await client.PostAsync("/api/v1/admin/barbers", form1);

        // Duplicate
        using var form2 = new MultipartFormDataContent();
        form2.Add(new StringContent("Barber Two"), "Name");
        form2.Add(new StringContent(email), "Email");
        form2.Add(new StringContent("Password123!"), "Password");
        var response = await client.PostAsync("/api/v1/admin/barbers", form2);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── PATCH /admin/barbers/{id}/deactivate & activate ──────────────────────

    [Fact]
    public async Task DeactivateAndActivateBarber_AsAdmin_Returns204()
    {
        // Seed a barber via admin endpoint
        var adminClient = CreateAdminClient();
        var email = $"toggle-barber-{Guid.NewGuid()}@test.com";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Toggle Barbeiro"), "Name");
        form.Add(new StringContent(email), "Email");
        form.Add(new StringContent("Password123!"), "Password");
        var createResp = await adminClient.PostAsync("/api/v1/admin/barbers", form);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the barber id from the barber login
        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var barberId = loginBody.GetProperty("barberId").GetGuid();

        // Deactivate
        var deactivateResp = await adminClient.PatchAsync($"/api/v1/admin/barbers/{barberId}/deactivate", null);
        deactivateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Activate
        var activateResp = await adminClient.PatchAsync($"/api/v1/admin/barbers/{barberId}/activate", null);
        activateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeactivateBarber_NonExistent_Returns404()
    {
        var client = CreateAdminClient();
        var response = await client.PatchAsync($"/api/v1/admin/barbers/{Guid.NewGuid()}/deactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH /admin/profile/password ─────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns401()
    {
        // Login as admin to get a real token with the correct UserId
        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@test.com", password = "AdminTest123!" });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = loginBody.GetProperty("accessToken").GetString()!;

        var authClient = _fixture.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await authClient.PatchAsJsonAsync("/api/v1/admin/profile/password",
            new { currentPassword = "WrongPassword!", newPassword = "NewPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
