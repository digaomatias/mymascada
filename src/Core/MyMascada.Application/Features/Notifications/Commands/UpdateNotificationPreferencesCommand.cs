using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Notifications.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Notifications.Commands;

public class UpdateNotificationPreferencesCommand : IRequest<NotificationPreferenceDto>
{
    public Guid UserId { get; set; }
    public string? ChannelPreferences { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
    public string? QuietHoursTimezone { get; set; }
    public decimal? LargeTransactionThreshold { get; set; }
    public int? BudgetAlertPercentage { get; set; }
    public int? RunwayWarningMonths { get; set; }
}

public class UpdateNotificationPreferencesCommandHandler : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferenceDto>
{
    private readonly INotificationPreferenceRepository _repository;

    public UpdateNotificationPreferencesCommandHandler(INotificationPreferenceRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationPreferenceDto> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

        var preference = existing ?? new NotificationPreference { UserId = request.UserId };

        preference.ChannelPreferences = request.ChannelPreferences;
        preference.QuietHoursTimezone = request.QuietHoursTimezone;
        preference.LargeTransactionThreshold = request.LargeTransactionThreshold;
        preference.BudgetAlertPercentage = request.BudgetAlertPercentage;
        preference.RunwayWarningMonths = request.RunwayWarningMonths;

        if (TimeOnly.TryParse(request.QuietHoursStart, out var start))
            preference.QuietHoursStart = start;
        else
            preference.QuietHoursStart = null;

        if (TimeOnly.TryParse(request.QuietHoursEnd, out var end))
            preference.QuietHoursEnd = end;
        else
            preference.QuietHoursEnd = null;

        var saved = await _repository.CreateOrUpdateAsync(preference, cancellationToken);

        return new NotificationPreferenceDto
        {
            ChannelPreferences = saved.ChannelPreferences,
            QuietHoursStart = saved.QuietHoursStart?.ToString("HH:mm"),
            QuietHoursEnd = saved.QuietHoursEnd?.ToString("HH:mm"),
            QuietHoursTimezone = saved.QuietHoursTimezone,
            LargeTransactionThreshold = saved.LargeTransactionThreshold,
            BudgetAlertPercentage = saved.BudgetAlertPercentage,
            RunwayWarningMonths = saved.RunwayWarningMonths
        };
    }
}
