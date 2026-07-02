using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Admin;

public class AdminBlocksControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AdminBlocksControllerTests(WebAppFixture fixture) => _fixture = fixture;

    private HttpClient CreateAdminClient()
        => _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

    /// <summary>Seeds a barber via the admin endpoint and returns their barberId.</summary>
    private async Task<Guid> SeedBarberAndGetId()
    {
        var adminClient = CreateAdminClient();
        var email = $"block-barber-{Guid.NewGuid()}@test.com";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Block Test Barber"), "Name");
        form.Add(new StringContent(email), "Email");
        form.Add(new StringContent("Password123!"), "Password");
        var createResp = await adminClient.PostAsync("/api/v1/admin/barbers", form);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return loginBody.GetProperty("barberId").GetGuid();
    }

    // ── Authorization guards ──────────────────────────────────────────────────

    [Fact]
    public async Task GetBarberBlocks_Unauthenticated_Returns401()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/admin/barbers/{Guid.NewGuid()}/blocks");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBarberBlocks_AsBarber_Returns403()
    {
        var client = _fixture.CreateAuthenticatedClient("Barber", Guid.NewGuid(), Guid.NewGuid());
        var response = await client.GetAsync($"/api/v1/admin/barbers/{Guid.NewGuid()}/blocks");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /admin/barbers/{barberId}/blocks ──────────────────────────────────

    [Fact]
    public async Task GetBarberBlocks_AsAdmin_Returns200WithEmptyList()
    {
        var barberId = await SeedBarberAndGetId();
        var client = CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/admin/barbers/{barberId}/blocks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── POST + DELETE /admin/barbers/{barberId}/blocks ────────────────────────

    [Fact]
    public async Task CreateAndDeleteBlock_AsAdmin_Works()
    {
        var barberId = await SeedBarberAndGetId();
        var client = CreateAdminClient();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/barbers/{barberId}/blocks",
            new
            {
                startsAt = DateTime.UtcNow.AddDays(1).Date.AddHours(12),
                endsAt = DateTime.UtcNow.AddDays(1).Date.AddHours(13),
                description = "Almoço",
                isRecurring = false,
                recurrenceDays = (int?)null,
                recurrenceEndsAt = (DateTime?)null
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = created.GetProperty("id").GetGuid();
        id.Should().NotBeEmpty();

        var deleteResponse = await client.DeleteAsync($"/api/v1/admin/barbers/{barberId}/blocks/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateBlock_AsAdmin_AppearsInGetBlocks()
    {
        var barberId = await SeedBarberAndGetId();
        var client = CreateAdminClient();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/barbers/{barberId}/blocks",
            new
            {
                startsAt = DateTime.UtcNow.AddDays(2).Date.AddHours(9),
                endsAt = DateTime.UtcNow.AddDays(2).Date.AddHours(10),
                description = "Reunião",
                isRecurring = false,
                recurrenceDays = (int?)null,
                recurrenceEndsAt = (DateTime?)null
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetAsync($"/api/v1/admin/barbers/{barberId}/blocks");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        list.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }
}
