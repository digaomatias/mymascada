namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for accessing the current authenticated user's information
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ID from the authentication context
    /// </summary>
    /// <returns>The current user's ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated or user ID is invalid</exception>
    Guid GetUserId();
}
