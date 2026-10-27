namespace HvacGuard.Domain.Common;

/// <summary>
/// Thrown when an operation would violate a business rule (e.g. an invalid work order transition).
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>
/// Thrown when a requested entity does not exist.
/// </summary>
public sealed class NotFoundException(string entityName, object key)
    : DomainException($"{entityName} with id '{key}' was not found.");
