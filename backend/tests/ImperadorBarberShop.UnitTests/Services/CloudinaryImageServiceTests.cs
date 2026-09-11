using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Services;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace ImperadorBarberShop.UnitTests.Services;

public class CloudinaryImageServiceTests
{
    [Fact]
    public void Constructor_WithCloudinaryUnset_DoesNotThrow()
    {
        // AdminController resolve este serviço em todo request: se a construção falhar,
        // o painel admin inteiro cai junto
        var act = () => new CloudinaryImageService(Options.Create(new CloudinarySettings()));

        act.Should().NotThrow();
    }

    [Fact]
    public async Task UploadAsync_WithCloudinaryUnset_ExplainsThatPhotoUploadIsDisabled()
    {
        var service = new CloudinaryImageService(Options.Create(new CloudinarySettings()));

        var act = () => service.UploadAsync(new MemoryStream([1, 2, 3]), "foto.png", "image/png");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(CloudinaryImageService.NotConfiguredMessage);
    }
}
