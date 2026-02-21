using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Onboarding.DTOs;

namespace MyMascada.Application.Features.Onboarding.Queries;

public class GetOnboardingStatusQuery : IRequest<OnboardingStatusResponse>
{
    public Guid UserId { get; set; }
}

public class GetOnboardingStatusQueryHandler : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusResponse>
{
    private readonly IUserFinancialProfileRepository _profileRepository;

    public GetOnboardingStatusQueryHandler(IUserFinancialProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    public async Task<OnboardingStatusResponse> Handle(GetOnboardingStatusQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (profile == null || !profile.OnboardingCompleted)
        {
            return new OnboardingStatusResponse
            {
                IsComplete = false
            };
        }

        return new OnboardingStatusResponse
        {
            IsComplete = true,
            MonthlyIncome = profile.MonthlyIncome,
            MonthlyExpenses = profile.MonthlyExpenses,
            MonthlyAvailable = profile.MonthlyAvailable
        };
    }
}
