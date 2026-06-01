using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationPreference> CreateOrUpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}
