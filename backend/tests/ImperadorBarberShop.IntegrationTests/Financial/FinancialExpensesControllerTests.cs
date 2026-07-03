using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Financial;

public class FinancialExpensesControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinancialExpensesControllerTests(WebAppFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Logs in as the seeded admin (admin@test.com / AdminTest123!) to obtain a real JWT
    /// whose sub claim matches a User row in the DB — required because CreateExpense stores
    /// CreatedByUserId as a FK to Users.
    /// </summary>
    private async Task<HttpClient> AdminClientAsync()
    {
        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@test.com", password = "AdminTest123!" });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "seeded admin login must succeed");
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var token = body.GetProperty("accessToken").GetString()!;
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task CreateExpense_ValidPayload_Returns201()
    {
        var client = await AdminClientAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 150.00m, description = "Produto para cabelo", date = "2026-07-01" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateExpense_ZeroAmount_Returns400()
    {
        var client = await AdminClientAsync();
        var resp = await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 0m, description = "Test", date = "2026-07-01" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExpense_Unauthenticated_Returns401()
    {
        var resp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 50m, description = "Test", date = "2026-07-01" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteExpense_ExistingExpense_Returns204()
    {
        var client = await AdminClientAsync();
        var createResp = await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 75m, description = "Lâminas", date = "2026-07-02" });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var id = created.GetProperty("id").GetString();

        var deleteResp = await client.DeleteAsync($"/api/v1/admin/financial/expenses/{id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteExpense_NotFound_Returns404()
    {
        var client = await AdminClientAsync();
        var resp = await client.DeleteAsync($"/api/v1/admin/financial/expenses/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExpenses_FiltersByDateRange()
    {
        var client = await AdminClientAsync();
        await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 100m, description = "Dentro do período", date = "2026-07-15" });
        await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 50m, description = "Fora do período", date = "2026-06-01" });

        var resp = await client.GetAsync("/api/v1/admin/financial/expenses?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        list.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        var descriptions = Enumerable.Range(0, list.GetArrayLength())
            .Select(i => list[i].GetProperty("description").GetString())
            .ToList();
        descriptions.Should().NotContain("Fora do período");
    }
}
