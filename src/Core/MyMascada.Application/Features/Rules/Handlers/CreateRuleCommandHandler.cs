using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Rules.Commands;
using MyMascada.Application.Features.Rules.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Rules.Handlers;

public class CreateRuleCommandHandler : IRequestHandler<CreateRuleCommand, CategorizationRuleDto>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public CreateRuleCommandHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<CategorizationRuleDto> Handle(CreateRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new CategorizationRule
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Pattern = request.Pattern,
            IsCaseSensitive = request.IsCaseSensitive,
            Priority = request.Priority,
            IsActive = request.IsActive,
            IsAiGenerated = false,
            ConfidenceScore = request.ConfidenceScore,
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            AccountTypes = request.AccountTypes,
            CategoryId = request.CategoryId,
            UserId = request.UserId,
            Logic = request.Logic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add conditions if provided
        foreach (var conditionDto in request.Conditions)
        {
            var condition = new RuleCondition
            {
                Field = conditionDto.Field,
                Operator = conditionDto.Operator,
                Value = conditionDto.Value,
                IsCaseSensitive = conditionDto.IsCaseSensitive,
                Order = conditionDto.Order,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            rule.Conditions.Add(condition);
        }

        var createdRule = await _ruleRepository.CreateRuleAsync(rule, cancellationToken);

        return new CategorizationRuleDto
        {
            Id = createdRule.Id,
            Name = createdRule.Name,
            Description = createdRule.Description,
            Type = createdRule.Type,
            Pattern = createdRule.Pattern,
            IsCaseSensitive = createdRule.IsCaseSensitive,
            Priority = createdRule.Priority,
            IsActive = createdRule.IsActive,
            IsAiGenerated = createdRule.IsAiGenerated,
            ConfidenceScore = createdRule.ConfidenceScore,
            MatchCount = createdRule.MatchCount,
            CorrectionCount = createdRule.CorrectionCount,
            MinAmount = createdRule.MinAmount,
            MaxAmount = createdRule.MaxAmount,
            AccountTypes = createdRule.AccountTypes,
            CategoryId = createdRule.CategoryId,
            CategoryName = createdRule.Category?.Name ?? "",
            Logic = createdRule.Logic,
            AccuracyRate = createdRule.GetAccuracyRate(),
            CreatedAt = createdRule.CreatedAt,
            UpdatedAt = createdRule.UpdatedAt,
            Conditions = createdRule.Conditions.Where(c => !c.IsDeleted).Select(c => new RuleConditionDto
            {
                Id = c.Id,
                Field = c.Field,
                Operator = c.Operator,
                Value = c.Value,
                IsCaseSensitive = c.IsCaseSensitive,
                Order = c.Order
            }).ToList(),
            ApplicationCount = 0
        };
    }
}