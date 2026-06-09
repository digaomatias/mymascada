namespace MyMascada.Application.Common.Interfaces;

public interface IPushNotificationService
{
    /// <summary>
    /// Sends a push notification to all devices registered for the user.
    /// Fail-soft: logs and returns without throwing when push is not configured
    /// or delivery fails. Tokens reported as unregistered by FCM are pruned.
    /// </summary>
    /// <param name="userId">Target user.</param>
    /// <param name="title">Human-readable notification title.</param>
    /// <param name="body">Human-readable notification body.</param>
    /// <param name="data">
    /// Data payload delivered to the app. The mobile client navigates using the
    /// "route" key (an app route path such as "/transactions").
    /// </param>
    Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
