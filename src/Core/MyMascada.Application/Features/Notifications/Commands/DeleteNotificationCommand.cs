using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Notifications.Commands;

public class DeleteNotificationCommand : IRequest
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand>
{
    private readonly INotificationRepository _repository;

    public DeleteNotificationCommandHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.NotificationId, request.UserId, cancellationToken);
    }
}
