namespace MyMascada.Infrastructure.Services.Push;

/// <summary>
/// Thin abstraction over the Firebase Cloud Messaging SDK so that push
/// dispatch and token-pruning logic can be unit tested without Firebase.
/// </summary>
public interface IFcmClient
{
    /// <summary>
    /// Whether Firebase credentials are configured. When false, sends are skipped
    /// (fail-soft for development environments without Firebase).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sends a notification message to the given device tokens.
    /// Returns one result per token, in the same order as <paramref name="tokens"/>.
    /// </summary>
    Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}

/// <summary>Per-token outcome of an FCM multicast send.</summary>
public sealed class FcmSendResult
{
    public required string Token { get; init; }
    public required bool Success { get; init; }

    /// <summary>
    /// True when FCM reported the token as unregistered/invalid and it should
    /// be pruned from the device registry.
    /// </summary>
    public bool IsTokenInvalid { get; init; }

    public string? Error { get; init; }
}
