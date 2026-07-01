using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Services;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class ServiceAddonCommandHandlerTests
{
    private readonly IServiceRepository _serviceRepo = Substitute.For<IServiceRepository>();
    private readonly IServiceAddonRepository _addonRepo = Substitute.For<IServiceAddonRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task AddAddon_SameId_Throws()
    {
        var id = Guid.NewGuid();
        var handler = new AddServiceAddonCommandHandler(_serviceRepo, _addonRepo, _uow);
        var act = () => handler.Handle(new AddServiceAddonCommand(id, id), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot*");
    }

    [Fact]
    public async Task AddAddon_AlreadyLinked_ThrowsConflict()
    {
        var parentId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var existingAddon = ServiceAddon.Create(parentId, addonId);
        _addonRepo.GetAsync(parentId, addonId, Arg.Any<CancellationToken>()).Returns(existingAddon);

        var handler = new AddServiceAddonCommandHandler(_serviceRepo, _addonRepo, _uow);
        var act = () => handler.Handle(new AddServiceAddonCommand(parentId, addonId), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already*");
    }
}
