using MyMascada.Application.Features.UserData.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for exporting all user data for LGPD/GDPR compliance.
/// </summary>
public interface IUserDataExportService
{
    /// <summary>
    /// Exports all personal data associated with a user account.
    /// </summary>
    /// <param name="userId">The user ID to export data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete user data export DTO.</returns>
    Task<UserDataExportDto> ExportUserDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
