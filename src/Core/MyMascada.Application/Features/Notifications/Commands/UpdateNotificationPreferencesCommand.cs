using FluentValidation;
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

public class UpdateNotificationPreferencesCommandValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        When(x => x.QuietHoursStart != null, () =>
        {
            RuleFor(x => x.QuietHoursStart)
                .Must(s => TimeOnly.TryParse(s, out _))
                .WithMessage("QuietHoursStart must be a valid time in HH:mm format.");
        });

        When(x => x.QuietHoursEnd != null, () =>
        {
            RuleFor(x => x.QuietHoursEnd)
                .Must(s => TimeOnly.TryParse(s, out _))
                .WithMessage("QuietHoursEnd must be a valid time in HH:mm format.");
        });

        When(x => x.BudgetAlertPercentage.HasValue, () =>
        {
            RuleFor(x => x.BudgetAlertPercentage!.Value)
                .InclusiveBetween(1, 100)
                .WithMessage("BudgetAlertPercentage must be between 1 and 100.");
        });

        When(x => x.RunwayWarningMonths.HasValue, () =>
        {
            RuleFor(x => x.RunwayWarningMonths!.Value)
                .InclusiveBetween(1, 24)
                .WithMessage("RunwayWarningMonths must be between 1 and 24.");
        });

        When(x => x.LargeTransactionThreshold.HasValue, () =>
        {
            RuleFor(x => x.LargeTransactionThreshold!.Value)
                .GreaterThan(0)
                .WithMessage("LargeTransactionThreshold must be greater than 0.");
        });
    }
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
        // Parse quiet hours first — validation ensures these are valid if provided
        TimeOnly? quietHoursStart = request.QuietHoursStart != null
            ? TimeOnly.Parse(request.QuietHoursStart)
            : null;

        TimeOnly? quietHoursEnd = request.QuietHoursEnd != null
            ? TimeOnly.Parse(request.QuietHoursEnd)
            : null;

        // CreateOrUpdateAsync handles the single-read upsert internally
        var preference = new NotificationPreference
        {
            UserId = request.UserId,
            ChannelPreferences = request.ChannelPreferences,
            QuietHoursStart = quietHoursStart,
            QuietHoursEnd = quietHoursEnd,
            QuietHoursTimezone = request.QuietHoursTimezone,
            LargeTransactionThreshold = request.LargeTransactionThreshold,
            BudgetAlertPercentage = request.BudgetAlertPercentage,
            RunwayWarningMonths = request.RunwayWarningMonths
        };

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
