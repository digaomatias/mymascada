using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Devices.DTOs;

namespace MyMascada.Application.Features.Devices.Commands;

/// <summary>
/// Idempotently registers (upserts) an FCM device token for push notifications.
/// </summary>
public class RegisterDeviceCommand : IRequest<DeviceDto>
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

public class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, DeviceDto>
{
    /// <summary>FCM tokens are well under this; guards the DB column limit.</summary>
    public const int MaxTokenLength = 512;
    public const int MaxPlatformLength = 20;

    private static readonly string[] AllowedPlatforms = { "ios", "android" };

    private readonly IUserDeviceRepository _repository;

    public RegisterDeviceCommandHandler(IUserDeviceRepository repository)
    {
        _repository = repository;
    }

    public async Task<DeviceDto> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Device token is required.", nameof(request));
        if (token.Length > MaxTokenLength)
            throw new ArgumentException($"Device token exceeds maximum length of {MaxTokenLength}.", nameof(request));

        var platform = request.Platform?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(platform) || platform.Length > MaxPlatformLength || !AllowedPlatforms.Contains(platform))
            throw new ArgumentException("Platform must be one of: ios, android.", nameof(request));

        var device = await _repository.UpsertAsync(request.UserId, token, platform, cancellationToken);

        return new DeviceDto
        {
            Id = device.Id,
            Platform = device.Platform,
            LastSeenAt = device.LastSeenAt
        };
    }
}
