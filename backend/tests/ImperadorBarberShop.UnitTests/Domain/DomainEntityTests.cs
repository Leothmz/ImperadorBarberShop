using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.UnitTests.Domain;

public class DomainEntityTests
{
    [Fact]
    public void UserRole_Admin_HasValue2()
        => ((int)UserRole.Admin).Should().Be(2);

    [Fact]
    public void User_CreateAdmin_SetsRoleAdmin()
    {
        var user = User.CreateAdmin("Administrador", "admin@test.com", "hash");
        user.Role.Should().Be(UserRole.Admin);
        user.Name.Should().Be("Administrador");
    }

    [Fact]
    public void Barber_NewBarber_IsActiveByDefault()
    {
        var barber = Barber.Create(Guid.NewGuid());
        barber.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Barber_Deactivate_SetsIsActiveFalse()
    {
        var barber = Barber.Create(Guid.NewGuid());
        barber.Deactivate();
        barber.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ServiceAddon_Create_SameId_Throws()
    {
        var id = Guid.NewGuid();
        var act = () => ServiceAddon.Create(id, id);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ServiceAddon_Create_DifferentIds_Succeeds()
    {
        var addon = ServiceAddon.Create(Guid.NewGuid(), Guid.NewGuid());
        addon.Should().NotBeNull();
    }
}
