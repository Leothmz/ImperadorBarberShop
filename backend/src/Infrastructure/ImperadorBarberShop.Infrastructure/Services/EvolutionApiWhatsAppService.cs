using System.Net.Http.Json;
using System.Text.Json;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;

namespace ImperadorBarberShop.Infrastructure.Services;

public class EvolutionApiWhatsAppService : IWhatsAppService
{
    private readonly HttpClient _http;
    private readonly IAppSettingsRepository _settings;

    public EvolutionApiWhatsAppService(HttpClient http, IAppSettingsRepository settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/message/sendText/{instance}");
        req.Headers.Add("apikey", apiKey);
        req.Content = JsonContent.Create(new { number = phone, text = message });
        using var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WhatsAppStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/instance/connectionState/{instance}");
        req.Headers.Add("apikey", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var inst = doc.RootElement.GetProperty("instance");
        var state = inst.GetProperty("state").GetString();
        var phone = inst.TryGetProperty("profileName", out var p) ? p.GetString() : null;

        var status = state switch
        {
            "open"       => WhatsAppConnectionStatus.Connected,
            "connecting" => WhatsAppConnectionStatus.QrRequired,
            _            => WhatsAppConnectionStatus.Disconnected
        };
        return new WhatsAppStatus(status, phone);
    }

    public async Task<WhatsAppQr> GetQrCodeAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/instance/connect/{instance}");
        req.Headers.Add("apikey", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return new WhatsAppQr(doc.RootElement.GetProperty("base64").GetString()!);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey, instance) = await GetConfigAsync(ct);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/instance/logout/{instance}");
        req.Headers.Add("apikey", apiKey);
        using var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<(string baseUrl, string apiKey, string instance)> GetConfigAsync(CancellationToken ct)
    {
        var baseUrl  = await _settings.GetAsync("whatsapp:evolutionApiUrl",  ct);
        var apiKey   = await _settings.GetAsync("whatsapp:evolutionApiKey",  ct);
        var instance = await _settings.GetAsync("whatsapp:instanceName",     ct);
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(instance))
            throw new InvalidOperationException(
                "WhatsApp not configured. Set whatsapp:evolutionApiUrl, whatsapp:evolutionApiKey, whatsapp:instanceName in AppSettings.");
        return (baseUrl.TrimEnd('/'), apiKey, instance);
    }
}
