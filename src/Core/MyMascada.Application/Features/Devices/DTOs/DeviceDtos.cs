namespace MyMascada.Application.Features.Devices.DTOs;

public class RegisterDeviceRequest
{
    /// <summary>Firebase Cloud Messaging registration token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Device platform, e.g. "ios" or "android".</summary>
    public string Platform { get; set; } = string.Empty;
}

public class UnregisterDeviceRequest
{
    /// <summary>
    /// Firebase Cloud Messaging registration token to remove. Sent in the body
    /// (not the URL) so the token never appears in edge/proxy request logs.
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

public class DeviceDto
{
    public Guid Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; }
}
