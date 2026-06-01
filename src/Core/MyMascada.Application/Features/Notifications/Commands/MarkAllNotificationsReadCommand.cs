using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Notifications.Commands;

public class MarkAllNotificationsReadCommand : IRequest
{
    public Guid UserId { get; set; }
}

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly INotificationRepository _repository;

    public MarkAllNotificationsReadCommandHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await _repository.MarkAllAsReadAsync(request.UserId, cancellationToken);
    }
}
