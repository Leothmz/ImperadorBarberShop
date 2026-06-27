using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class AppointmentTests
{
    [Fact]
    public void Create_ValidInput_IsBornAccepted_WithUniqueAccessToken()
    {
        var a1 = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        var a2 = Appointment.Create("Maria", "+5511999990001", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        a1.Status.Should().Be(AppointmentStatus.Accepted);
        a1.ClientName.Should().Be("João");
        a1.ClientPhone.Should().Be("+5511999990000");
        a1.AccessToken.Should().NotBeNullOrEmpty();
        a1.AccessToken.Should().NotBe(a2.AccessToken);
    }

    [Fact]
    public void Cancel_WhenAccepted_SetsCancelled()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        appointment.Cancel();

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Cancel();

        var act = () => appointment.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_WhenAccepted_SetsCompleted()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        appointment.Complete();

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }
}
