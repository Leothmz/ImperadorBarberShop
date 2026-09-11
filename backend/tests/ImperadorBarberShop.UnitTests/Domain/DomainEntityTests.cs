using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.ValueObjects;

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

public class AppointmentPaymentMethodTests
{
    [Fact]
    public void Complete_WithPaymentMethod_SetsMethodAndPaidAt()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        appt.Complete(AppointmentPayment.Normal(PaymentMethod.Pix));
        appt.Status.Should().Be(AppointmentStatus.Completed);
        appt.PaymentMethod.Should().Be(PaymentMethod.Pix);
        appt.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithoutPaymentMethod_LeavesMethodNull()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        appt.Complete();
        appt.Status.Should().Be(AppointmentStatus.Completed);
        appt.PaymentMethod.Should().BeNull();
        appt.PaidAt.Should().BeNull();
    }

    [Fact]
    public void SetPaymentMethod_OnCompleted_SetsMethod()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        appt.Complete();
        appt.SetPayment(AppointmentPayment.Normal(PaymentMethod.Dinheiro));
        appt.PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
        appt.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void SetPaymentMethod_OnAccepted_ThrowsInvalidOperationException()
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, [Service.Create("Corte", "Desc", 30, 35m)]);
        var act = () => appt.SetPayment(AppointmentPayment.Normal(PaymentMethod.Pix));
        act.Should().Throw<InvalidOperationException>();
    }
}
