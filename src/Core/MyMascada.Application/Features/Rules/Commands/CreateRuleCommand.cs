using MediatR;
using MyMascada.Application.Features.Rules.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Rules.Commands;

public class CreateRuleCommand : IRequest<CategorizationRuleDto>
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RuleType Type { get; set; } = RuleType.Contains;
    public string Pattern { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; } = false;
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public double? ConfidenceScore { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? AccountTypes { get; set; }
    public int CategoryId { get; set; }
    public RuleLogic Logic { get; set; } = RuleLogic.All;
    public List<CreateRuleConditionDto> Conditions { get; set; } = new();
}

public class CreateRuleConditionDto
{
    public RuleConditionField Field { get; set; }
    public RuleConditionOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; } = false;
    public int Order { get; set; }
}

public class UpdateRuleCommand : IRequest<CategorizationRuleDto>
{
    public int RuleId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RuleType Type { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public double? ConfidenceScore { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? AccountTypes { get; set; }
    public int CategoryId { get; set; }
    public RuleLogic Logic { get; set; }
    public List<UpdateRuleConditionDto> Conditions { get; set; } = new();
}

public class UpdateRuleConditionDto
{
    public int? Id { get; set; } // Null for new conditions
    public RuleConditionField Field { get; set; }
    public RuleConditionOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; }
    public int Order { get; set; }
    public bool IsDeleted { get; set; } = false;
}

public class DeleteRuleCommand : IRequest<bool>
{
    public int RuleId { get; set; }
    public Guid UserId { get; set; }
}

public class TestRuleCommand : IRequest<RuleTestResultDto>
{
    public int RuleId { get; set; }
    public Guid UserId { get; set; }
    public int MaxResults { get; set; } = 50;
}

public class ApplyRulesCommand : IRequest<RuleMatchResultDto?>
{
    public Guid UserId { get; set; }
    public int TransactionId { get; set; }
}

public class ApplyRulesBatchCommand : IRequest<IEnumerable<TransactionRuleMatchDto>>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
}

public class UpdateRulePrioritiesCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public Dictionary<int, int> RulePriorities { get; set; } = new();
}

public class RecordCorrectionCommand : IRequest
{
    public int RuleId { get; set; }
    public Guid UserId { get; set; }
    public int TransactionId { get; set; }
    public int NewCategoryId { get; set; }
}

public class CreateRuleFromSuggestionCommand : IRequest<CategorizationRuleDto>
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Type { get; set; } = "Contains";
    public int CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
}