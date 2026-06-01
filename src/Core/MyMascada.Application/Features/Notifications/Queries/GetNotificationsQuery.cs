using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Notifications.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Notifications.Queries;

public class GetNotificationsQuery : IRequest<NotificationListResponse>
{
    public Guid UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public NotificationType? Type { get; set; }
    public bool? IsRead { get; set; }
}

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, NotificationListResponse>
{
    private readonly INotificationRepository _repository;

    public GetNotificationsQueryHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationListResponse> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            request.Type,
            request.IsRead,
            cancellationToken);

        return new NotificationListResponse
        {
            Items = items.Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Priority = n.Priority.ToString(),
                Title = n.Title,
                Body = n.Body,
                Data = n.Data,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            }).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
