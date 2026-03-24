using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Notifications.DTOs;

namespace MyMascada.Application.Features.Notifications.Queries;

public class GetNotificationPreferencesQuery : IRequest<NotificationPreferenceDto>
{
    public Guid UserId { get; set; }
}

public class GetNotificationPreferencesQueryHandler : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferenceDto>
{
    private readonly INotificationPreferenceRepository _repository;

    public GetNotificationPreferencesQueryHandler(INotificationPreferenceRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationPreferenceDto> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var pref = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (pref == null)
        {
            // Return defaults
            return new NotificationPreferenceDto
            {
                BudgetAlertPercentage = 80,
                RunwayWarningMonths = 3,
                LargeTransactionThreshold = 500
            };
        }

        return new NotificationPreferenceDto
        {
            ChannelPreferences = pref.ChannelPreferences,
            QuietHoursStart = pref.QuietHoursStart?.ToString("HH:mm"),
            QuietHoursEnd = pref.QuietHoursEnd?.ToString("HH:mm"),
            QuietHoursTimezone = pref.QuietHoursTimezone,
            LargeTransactionThreshold = pref.LargeTransactionThreshold,
            BudgetAlertPercentage = pref.BudgetAlertPercentage,
            RunwayWarningMonths = pref.RunwayWarningMonths
        };
    }
}
