using AutoMapper;
using FluentAssertions;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Application.Queries.Services;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class GetServicesQueryHandlerTests
{
    private readonly IServiceRepository _serviceRepo = Substitute.For<IServiceRepository>();
    private readonly IServiceAddonRepository _addonRepo = Substitute.For<IServiceAddonRepository>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    /// <summary>
    /// Sets the EF Core navigation property AddonService on a ServiceAddon via reflection,
    /// since the property has a private setter that EF Core populates at runtime.
    /// </summary>
    private static ServiceAddon WithAddonService(ServiceAddon link, Service addonService)
    {
        var prop = typeof(ServiceAddon).GetProperty("AddonService")!;
        prop.SetValue(link, addonService);
        return link;
    }

    [Fact]
    public async Task Handle_FiltersOutInactiveAddons()
    {
        // Arrange
        var parentService = Service.Create("Parent", "desc", 30, 35m);
        var activeAddon = Service.Create("Active Addon", "desc", 20, 25m);
        var inactiveAddon = Service.Create("Inactive Addon", "desc", 15, 15m);
        inactiveAddon.Deactivate();

        var addonLinks = new List<ServiceAddon>
        {
            WithAddonService(ServiceAddon.Create(parentService.Id, activeAddon.Id), activeAddon),
            WithAddonService(ServiceAddon.Create(parentService.Id, inactiveAddon.Id), inactiveAddon),
        };

        _serviceRepo
            .GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Service> { parentService });

        _addonRepo
            .GetByParentIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(addonLinks);

        _mapper
            .Map<ServiceDto>(Arg.Any<Service>())
            .Returns(c =>
            {
                var s = c.Arg<Service>();
                return new ServiceDto { Id = s.Id, Name = s.Name, Description = "", DurationMinutes = s.DurationMinutes, Price = s.Price, IsActive = s.IsActive };
            });

        var handler = new GetServicesQueryHandler(_serviceRepo, _addonRepo, _mapper);

        // Act
        var result = await handler.Handle(new GetServicesQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Addons.Should().HaveCount(1);
        result[0].Addons[0].Name.Should().Be("Active Addon");
    }

    [Fact]
    public async Task Handle_NoAddons_ReturnsEmptyAddonList()
    {
        var service = Service.Create("Barba", "desc", 20, 25m);

        _serviceRepo
            .GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });

        _addonRepo
            .GetByParentIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ServiceAddon>());

        _mapper
            .Map<ServiceDto>(Arg.Any<Service>())
            .Returns(c =>
            {
                var s = c.Arg<Service>();
                return new ServiceDto { Id = s.Id, Name = s.Name, Description = "", DurationMinutes = s.DurationMinutes, Price = s.Price, IsActive = s.IsActive };
            });

        var handler = new GetServicesQueryHandler(_serviceRepo, _addonRepo, _mapper);

        var result = await handler.Handle(new GetServicesQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Addons.Should().BeEmpty();
    }
}
