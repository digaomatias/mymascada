using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Rules.DTOs;
using MyMascada.Application.Features.Rules.Queries;

namespace MyMascada.Application.Features.Rules.Handlers;

public class GetRulesQueryHandler : IRequestHandler<GetRulesQuery, IEnumerable<CategorizationRuleDto>>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public GetRulesQueryHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<IEnumerable<CategorizationRuleDto>> Handle(GetRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = request.IncludeInactive 
            ? await _ruleRepository.GetAllRulesForUserAsync(request.UserId, cancellationToken)
            : await _ruleRepository.GetActiveRulesForUserAsync(request.UserId, cancellationToken);

        return rules.Select(rule => new CategorizationRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            Type = rule.Type,
            Pattern = rule.Pattern,
            IsCaseSensitive = rule.IsCaseSensitive,
            Priority = rule.Priority,
            IsActive = rule.IsActive,
            IsAiGenerated = rule.IsAiGenerated,
            ConfidenceScore = rule.ConfidenceScore,
            MatchCount = rule.MatchCount,
            CorrectionCount = rule.CorrectionCount,
            MinAmount = rule.MinAmount,
            MaxAmount = rule.MaxAmount,
            AccountTypes = rule.AccountTypes,
            CategoryId = rule.CategoryId,
            CategoryName = rule.Category?.Name ?? "",
            Logic = rule.Logic,
            AccuracyRate = rule.GetAccuracyRate(),
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            Conditions = rule.Conditions.Where(c => !c.IsDeleted).Select(c => new RuleConditionDto
            {
                Id = c.Id,
                Field = c.Field,
                Operator = c.Operator,
                Value = c.Value,
                IsCaseSensitive = c.IsCaseSensitive,
                Order = c.Order
            }).ToList(),
            ApplicationCount = rule.Applications.Count
        });
    }
}

public class GetRuleByIdQueryHandler : IRequestHandler<GetRuleByIdQuery, CategorizationRuleDto?>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public GetRuleByIdQueryHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<CategorizationRuleDto?> Handle(GetRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetRuleByIdAsync(request.RuleId, request.UserId, cancellationToken);
        
        if (rule == null)
            return null;

        return new CategorizationRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            Type = rule.Type,
            Pattern = rule.Pattern,
            IsCaseSensitive = rule.IsCaseSensitive,
            Priority = rule.Priority,
            IsActive = rule.IsActive,
            IsAiGenerated = rule.IsAiGenerated,
            ConfidenceScore = rule.ConfidenceScore,
            MatchCount = rule.MatchCount,
            CorrectionCount = rule.CorrectionCount,
            MinAmount = rule.MinAmount,
            MaxAmount = rule.MaxAmount,
            AccountTypes = rule.AccountTypes,
            CategoryId = rule.CategoryId,
            CategoryName = rule.Category?.Name ?? "",
            Logic = rule.Logic,
            AccuracyRate = rule.GetAccuracyRate(),
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            Conditions = rule.Conditions.Where(c => !c.IsDeleted).Select(c => new RuleConditionDto
            {
                Id = c.Id,
                Field = c.Field,
                Operator = c.Operator,
                Value = c.Value,
                IsCaseSensitive = c.IsCaseSensitive,
                Order = c.Order
            }).ToList(),
            ApplicationCount = rule.Applications.Count
        };
    }
}

public class GetRuleStatisticsQueryHandler : IRequestHandler<GetRuleStatisticsQuery, RuleStatisticsDto>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public GetRuleStatisticsQueryHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<RuleStatisticsDto> Handle(GetRuleStatisticsQuery request, CancellationToken cancellationToken)
    {
        var ruleStats = await _ruleRepository.GetRuleStatisticsAsync(request.UserId, cancellationToken);
        var allRules = await _ruleRepository.GetAllRulesForUserAsync(request.UserId, cancellationToken);

        var totalRules = allRules.Count();
        var activeRules = allRules.Count(r => r.IsActive);
        var totalApplications = ruleStats.Values.Sum(s => s.MatchCount);
        var totalCorrections = ruleStats.Values.Sum(s => s.CorrectionCount);
        var overallAccuracy = totalApplications > 0 ? (double)(totalApplications - totalCorrections) / totalApplications * 100 : 0;

        var topPerforming = ruleStats
            .Where(rs => rs.Value.MatchCount > 0)
            .OrderByDescending(rs => rs.Value.AccuracyRate)
            .Take(5)
            .Select(rs => {
                var rule = allRules.First(r => r.Id == rs.Key);
                return new RulePerformanceDto
                {
                    RuleId = rs.Key,
                    RuleName = rule.Name,
                    MatchCount = rs.Value.MatchCount,
                    CorrectionCount = rs.Value.CorrectionCount,
                    AccuracyRate = rs.Value.AccuracyRate,
                    LastUsed = rule.UpdatedAt
                };
            }).ToList();

        var poorPerforming = ruleStats
            .Where(rs => rs.Value.MatchCount > 0 && rs.Value.AccuracyRate < 80)
            .OrderBy(rs => rs.Value.AccuracyRate)
            .Take(5)
            .Select(rs => {
                var rule = allRules.First(r => r.Id == rs.Key);
                return new RulePerformanceDto
                {
                    RuleId = rs.Key,
                    RuleName = rule.Name,
                    MatchCount = rs.Value.MatchCount,
                    CorrectionCount = rs.Value.CorrectionCount,
                    AccuracyRate = rs.Value.AccuracyRate,
                    LastUsed = rule.UpdatedAt
                };
            }).ToList();

        return new RuleStatisticsDto
        {
            TotalRules = totalRules,
            ActiveRules = activeRules,
            TotalApplications = totalApplications,
            TotalCorrections = totalCorrections,
            OverallAccuracy = overallAccuracy,
            TopPerformingRules = topPerforming,
            PoorPerformingRules = poorPerforming
        };
    }
}

