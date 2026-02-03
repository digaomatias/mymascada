using MediatR;
using MyMascada.Application.Features.Rules.DTOs;

namespace MyMascada.Application.Features.Rules.Queries;

public class GetRulesQuery : IRequest<IEnumerable<CategorizationRuleDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeInactive { get; set; } = false;
}

public class GetRuleByIdQuery : IRequest<CategorizationRuleDto?>
{
    public int RuleId { get; set; }
    public Guid UserId { get; set; }
}

public class GetRuleStatisticsQuery : IRequest<RuleStatisticsDto>
{
    public Guid UserId { get; set; }
}