namespace ShopKeeper.Application.Common.Exceptions;

/// <summary>Maps to HTTP 404. Thrown when a resource doesn't exist within the caller's tenant scope.</summary>
public class NotFoundException(string entity, object key) : Exception($"{entity} with id '{key}' was not found.");

/// <summary>Maps to HTTP 403. Thrown when the caller is authenticated but lacks permission or tenant access.</summary>
public class ForbiddenAccessException(string message = "You do not have permission to perform this action.") : Exception(message);

/// <summary>Maps to HTTP 401. Thrown for invalid credentials or expired/revoked tokens.</summary>
public class AuthenticationException(string message) : Exception(message);

/// <summary>Maps to HTTP 409. Thrown for state conflicts, e.g. duplicate email on register.</summary>
public class ConflictException(string message) : Exception(message);
