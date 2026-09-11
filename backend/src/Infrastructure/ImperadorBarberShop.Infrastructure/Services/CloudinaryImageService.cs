using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace ImperadorBarberShop.Infrastructure.Services;

public class CloudinaryImageService : IImageService
{
    public const string NotConfiguredMessage =
        "O envio de fotos está desativado porque o Cloudinary não foi configurado no servidor. " +
        "Salve sem foto ou configure o Cloudinary.";

    private readonly CloudinarySettings _settings;
    private Cloudinary? _cloudinary;

    // O cliente só é montado no primeiro upload. O AdminController injeta este serviço
    // no construtor: montá-lo aqui fazia um Cloudinary sem configuração derrubar com 500
    // o painel admin inteiro, e não só o envio de fotos.
    public CloudinaryImageService(IOptions<CloudinarySettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var cloudinary = _cloudinary ??= CreateClient();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = "imperador-barber",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await cloudinary.UploadAsync(uploadParams);

        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }

    private Cloudinary CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_settings.CloudName)
            || string.IsNullOrWhiteSpace(_settings.ApiKey)
            || string.IsNullOrWhiteSpace(_settings.ApiSecret))
            throw new InvalidOperationException(NotConfiguredMessage);

        var account = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
        return new Cloudinary(account) { Api = { Secure = true } };
    }
}
