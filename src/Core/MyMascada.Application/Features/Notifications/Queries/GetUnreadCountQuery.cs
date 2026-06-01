using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Notifications.DTOs;

namespace MyMascada.Application.Features.Notifications.Queries;

public class GetUnreadCountQuery : IRequest<UnreadCountResponse>
{
    public Guid UserId { get; set; }
}

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, UnreadCountResponse>
{
    private readonly INotificationRepository _repository;

    public GetUnreadCountQueryHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<UnreadCountResponse> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var count = await _repository.GetUnreadCountAsync(request.UserId, cancellationToken);
        return new UnreadCountResponse { Count = count };
    }
}
