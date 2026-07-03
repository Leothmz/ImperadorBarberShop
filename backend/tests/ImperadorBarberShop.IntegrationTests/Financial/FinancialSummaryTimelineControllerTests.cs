using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Financial;

public class FinancialSummaryTimelineControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinancialSummaryTimelineControllerTests(WebAppFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Logs in as the seeded admin account to get a real JWT whose sub claim matches
    /// a User row in the DB — required for operations that store a FK to Users (e.g. CreateExpense).
    /// </summary>
    private async Task<HttpClient> AdminClientAsync()
    {
        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@test.com", password = "AdminTest123!" });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK, "seeded admin login must succeed");
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var token = body.GetProperty("accessToken").GetString()!;
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetSummary_IncludesTotalExpensesAndNetRevenue()
    {
        var client = await AdminClientAsync();
        await client.PostAsJsonAsync("/api/v1/admin/financial/expenses",
            new { amount = 100m, description = "Custo teste", date = "2026-07-10" });

        var resp = await client.GetAsync("/api/v1/admin/financial/summary?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.TryGetProperty("totalExpenses", out _).Should().BeTrue();
        body.TryGetProperty("netRevenue", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeline_DefaultGroupByDay_Returns200()
    {
        var client = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var resp = await client.GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetTimeline_GroupByMonth_Returns200()
    {
        var client = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var resp = await client.GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31&groupBy=month");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTimeline_InvalidGroupBy_Returns400()
    {
        var client = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var resp = await client.GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31&groupBy=invalid");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTimeline_Unauthenticated_Returns401()
    {
        var resp = await _fixture.CreateClient().GetAsync("/api/v1/admin/financial/timeline?from=2026-07-01&to=2026-07-31");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
