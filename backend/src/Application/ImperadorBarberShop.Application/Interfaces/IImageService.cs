namespace ImperadorBarberShop.Application.Interfaces;

public interface IImageService
{
    /// <summary>Uploads image and returns the public URL.</summary>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
}
