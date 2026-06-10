using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Devices.Commands;

/// <summary>
/// Removes an FCM device token registration (e.g. on logout). Idempotent —
/// succeeds even if the token is not registered.
/// </summary>
public class UnregisterDeviceCommand : IRequest
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class UnregisterDeviceCommandHandler : IRequestHandler<UnregisterDeviceCommand>
{
    private readonly IUserDeviceRepository _repository;

    public UnregisterDeviceCommandHandler(IUserDeviceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UnregisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Device token is required.", nameof(request));

        await _repository.DeleteByTokenAsync(request.UserId, token, cancellationToken);
    }
}
