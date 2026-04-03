using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Services;

public class ServicesControllerTests : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client;

    public ServicesControllerTests(WebAppFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetServices_Returns200WithSeededServices()
    {
        var response = await _client.GetAsync("/api/v1/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await response.Content.ReadFromJsonAsync<JsonElement[]>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        services.Should().NotBeNullOrEmpty();
        services!.Length.Should().BeGreaterThanOrEqualTo(6);
    }
}
