namespace ImperadorBarberShop.Application.DTOs;

public record BarberDto(
    Guid Id,
    Guid UserId,
    string Name,
    string Email,
    decimal AverageRating,
    List<BarberAvailabilityDto> Availability);

public record BarberAvailabilityDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);
