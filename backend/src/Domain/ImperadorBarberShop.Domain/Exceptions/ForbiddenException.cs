namespace ImperadorBarberShop.Domain.Exceptions;

/// <summary>
/// Thrown when a user attempts to access or modify a resource that belongs to another user (IDOR).
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
