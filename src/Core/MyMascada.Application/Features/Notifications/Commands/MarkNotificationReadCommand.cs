using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Notifications.Commands;

public class MarkNotificationReadCommand : IRequest
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
}

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly INotificationRepository _repository;

    public MarkNotificationReadCommandHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        await _repository.MarkAsReadAsync(request.NotificationId, request.UserId, cancellationToken);
    }
}
