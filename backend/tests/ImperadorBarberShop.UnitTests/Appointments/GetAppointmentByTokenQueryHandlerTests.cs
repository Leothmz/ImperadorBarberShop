using AutoMapper;
using FluentAssertions;
using ImperadorBarberShop.Application.Mappings;
using ImperadorBarberShop.Application.Queries.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class GetAppointmentByTokenQueryHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IMapper _mapper;
    private readonly GetAppointmentByTokenQueryHandler _handler;

    public GetAppointmentByTokenQueryHandlerTests()
    {
        // NOTE: AutoMapper 16.x's MapperConfiguration requires an ILoggerFactory argument
        // (the brief's single-arg constructor predates this API change).
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
        _handler = new GetAppointmentByTokenQueryHandler(_appointmentRepository, _mapper);
    }

    [Fact]
    public async Task Handle_TokenFound_ReturnsDto()
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>())
            .Returns(appointment);

        var result = await _handler.Handle(new GetAppointmentByTokenQuery(appointment.AccessToken), CancellationToken.None);

        result.Id.Should().Be(appointment.Id);
        result.ClientName.Should().Be("João");
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(new GetAppointmentByTokenQuery("bogus"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
