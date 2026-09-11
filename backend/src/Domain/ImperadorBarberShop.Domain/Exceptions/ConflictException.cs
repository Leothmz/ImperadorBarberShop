namespace ImperadorBarberShop.Domain.Exceptions;

/// <summary>
/// Thrown when the request collides with the current state of a resource — e.g. the
/// requested time slot was just taken. Maps to HTTP 409 Conflict, which the booking
/// UI turns into "pick another slot" instead of "try again".
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
