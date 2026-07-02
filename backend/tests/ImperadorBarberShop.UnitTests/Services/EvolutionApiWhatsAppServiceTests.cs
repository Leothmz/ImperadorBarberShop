using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using NSubstitute;
using System.Net;

namespace ImperadorBarberShop.UnitTests.Services;

public class EvolutionApiWhatsAppServiceTests
{
    private readonly IAppSettingsRepository _settingsRepo = Substitute.For<IAppSettingsRepository>();
    private readonly FakeHttpMessageHandler _fakeHandler = new();
    private readonly EvolutionApiWhatsAppService _svc;

    public EvolutionApiWhatsAppServiceTests()
    {
        _settingsRepo.GetAsync("whatsapp:evolutionApiUrl",  Arg.Any<CancellationToken>()).Returns("http://evo.local");
        _settingsRepo.GetAsync("whatsapp:evolutionApiKey",  Arg.Any<CancellationToken>()).Returns("key123");
        _settingsRepo.GetAsync("whatsapp:instanceName",     Arg.Any<CancellationToken>()).Returns("imperador");
        _svc = new EvolutionApiWhatsAppService(new HttpClient(_fakeHandler), _settingsRepo);
    }

    [Fact]
    public async Task SendAsync_PostsToCorrectEndpoint()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, "{}");
        await _svc.SendAsync("+5511999990000", "Olá!", CancellationToken.None);
        _fakeHandler.LastRequest!.RequestUri!.ToString()
            .Should().Be("http://evo.local/message/sendText/imperador");
        _fakeHandler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetStatusAsync_StateOpen_ReturnsConnected()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK,
            """{"instance":{"state":"open","profileName":"+5511999990001"}}""");
        var result = await _svc.GetStatusAsync(CancellationToken.None);
        result.Status.Should().Be(WhatsAppConnectionStatus.Connected);
        result.PhoneNumber.Should().Be("+5511999990001");
    }

    [Fact]
    public async Task GetStatusAsync_StateClose_ReturnsDisconnected()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, """{"instance":{"state":"close"}}""");
        var result = await _svc.GetStatusAsync(CancellationToken.None);
        result.Status.Should().Be(WhatsAppConnectionStatus.Disconnected);
    }

    [Fact]
    public async Task GetStatusAsync_StateConnecting_ReturnsQrRequired()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, """{"instance":{"state":"connecting"}}""");
        var result = await _svc.GetStatusAsync(CancellationToken.None);
        result.Status.Should().Be(WhatsAppConnectionStatus.QrRequired);
    }

    [Fact]
    public async Task GetQrCodeAsync_ReturnsBase64()
    {
        _fakeHandler.SetResponse(HttpStatusCode.OK, """{"base64":"data:image/png;base64,abc"}""");
        var result = await _svc.GetQrCodeAsync(CancellationToken.None);
        result.QrCode.Should().Be("data:image/png;base64,abc");
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage _response = new(HttpStatusCode.OK);
    public HttpRequestMessage? LastRequest { get; private set; }

    public void SetResponse(HttpStatusCode status, string json) =>
        _response = new HttpResponseMessage(status)
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(_response);
    }
}
