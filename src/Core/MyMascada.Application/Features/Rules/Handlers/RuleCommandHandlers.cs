using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Rules.Commands;
using MyMascada.Application.Features.Rules.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Rules.Handlers;

public class UpdateRuleCommandHandler : IRequestHandler<UpdateRuleCommand, CategorizationRuleDto>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public UpdateRuleCommandHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<CategorizationRuleDto> Handle(UpdateRuleCommand request, CancellationToken cancellationToken)
    {
        var existingRule = await _ruleRepository.GetRuleByIdAsync(request.RuleId, request.UserId, cancellationToken);
        if (existingRule == null)
            throw new ArgumentException($"Rule with ID {request.RuleId} not found");

        // Update rule properties
        existingRule.Name = request.Name;
        existingRule.Description = request.Description;
        existingRule.Type = request.Type;
        existingRule.Pattern = request.Pattern;
        existingRule.IsCaseSensitive = request.IsCaseSensitive;
        existingRule.Priority = request.Priority;
        existingRule.IsActive = request.IsActive;
        existingRule.ConfidenceScore = request.ConfidenceScore;
        existingRule.MinAmount = request.MinAmount;
        existingRule.MaxAmount = request.MaxAmount;
        existingRule.AccountTypes = request.AccountTypes;
        existingRule.CategoryId = request.CategoryId;
        existingRule.Logic = request.Logic;

        // Update conditions (simple approach: remove all and re-add)
        existingRule.Conditions.Clear();
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
            existingRule.Conditions.Add(condition);
        }

        var updatedRule = await _ruleRepository.UpdateRuleAsync(existingRule, cancellationToken);

        return new CategorizationRuleDto
        {
            Id = updatedRule.Id,
            Name = updatedRule.Name,
            Description = updatedRule.Description,
            Type = updatedRule.Type,
            Pattern = updatedRule.Pattern,
            IsCaseSensitive = updatedRule.IsCaseSensitive,
            Priority = updatedRule.Priority,
            IsActive = updatedRule.IsActive,
            IsAiGenerated = updatedRule.IsAiGenerated,
            ConfidenceScore = updatedRule.ConfidenceScore,
            MatchCount = updatedRule.MatchCount,
            CorrectionCount = updatedRule.CorrectionCount,
            MinAmount = updatedRule.MinAmount,
            MaxAmount = updatedRule.MaxAmount,
            AccountTypes = updatedRule.AccountTypes,
            CategoryId = updatedRule.CategoryId,
            CategoryName = updatedRule.Category?.Name ?? "",
            Logic = updatedRule.Logic,
            AccuracyRate = updatedRule.GetAccuracyRate(),
            CreatedAt = updatedRule.CreatedAt,
            UpdatedAt = updatedRule.UpdatedAt,
            Conditions = updatedRule.Conditions.Where(c => !c.IsDeleted).Select(c => new RuleConditionDto
            {
                Id = c.Id,
                Field = c.Field,
                Operator = c.Operator,
                Value = c.Value,
                IsCaseSensitive = c.IsCaseSensitive,
                Order = c.Order
            }).ToList(),
            ApplicationCount = updatedRule.Applications.Count
        };
    }
}

public class DeleteRuleCommandHandler : IRequestHandler<DeleteRuleCommand, bool>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public DeleteRuleCommandHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<bool> Handle(DeleteRuleCommand request, CancellationToken cancellationToken)
    {
        await _ruleRepository.DeleteRuleAsync(request.RuleId, request.UserId, cancellationToken);
        return true;
    }
}

public class TestRuleCommandHandler : IRequestHandler<TestRuleCommand, RuleTestResultDto>
{
    private readonly ICategorizationRuleRepository _ruleRepository;
    private readonly ITransactionRepository _transactionRepository;

    public TestRuleCommandHandler(ICategorizationRuleRepository ruleRepository, ITransactionRepository transactionRepository)
    {
        _ruleRepository = ruleRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<RuleTestResultDto> Handle(TestRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetRuleByIdAsync(request.RuleId, request.UserId, cancellationToken);
        if (rule == null)
            throw new ArgumentException($"Rule with ID {request.RuleId} not found");

        // Get recent transactions for the user
        var recentTransactions = await _transactionRepository.GetRecentTransactionsAsync(
            request.UserId, 
            Math.Min(request.MaxResults * 10, 500)); // Get more than needed to find matches

        var matchingTransactions = new List<MatchingTransactionDto>();

        foreach (var transaction in recentTransactions)
        {
            if (matchingTransactions.Count >= request.MaxResults)
                break;

            // Apply rule matching logic
            if (DoesTransactionMatchRule(transaction, rule))
            {
                var currentCategoryName = transaction.Category?.Name ?? "Uncategorized";
                var suggestedCategoryName = rule.Category?.Name ?? "Target Category";
                var wouldChangeCategory = transaction.CategoryId != rule.CategoryId;

                matchingTransactions.Add(new MatchingTransactionDto
                {
                    Id = transaction.Id,
                    Description = transaction.Description ?? "",
                    Amount = transaction.Amount,
                    TransactionDate = transaction.TransactionDate,
                    AccountName = transaction.Account?.Name ?? "Unknown Account",
                    CurrentCategoryName = currentCategoryName,
                    SuggestedCategoryName = suggestedCategoryName,
                    WouldChangeCategory = wouldChangeCategory
                });
            }
        }

        var summary = $"Found {matchingTransactions.Count} matching transactions. " +
                     $"{matchingTransactions.Count(m => m.WouldChangeCategory)} would be recategorized.";

        return new RuleTestResultDto
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            TotalMatches = matchingTransactions.Count,
            MatchingTransactions = matchingTransactions,
            TestedAt = DateTime.UtcNow,
            TestSummary = summary
        };
    }

    private bool DoesTransactionMatchRule(Transaction transaction, CategorizationRule rule)
    {
        // Apply amount filters if specified
        if (rule.MinAmount.HasValue && transaction.Amount < rule.MinAmount.Value)
            return false;
        
        if (rule.MaxAmount.HasValue && transaction.Amount > rule.MaxAmount.Value)
            return false;

        // Apply account type filter if specified
        if (!string.IsNullOrEmpty(rule.AccountTypes))
        {
            var allowedAccountTypes = rule.AccountTypes.Split(',').Select(t => t.Trim()).ToHashSet();
            var transactionAccountType = transaction.Account?.Type.ToString() ?? "";
            if (!allowedAccountTypes.Contains(transactionAccountType))
                return false;
        }

        // Apply pattern matching based on rule type
        var description = transaction.Description ?? "";
        var pattern = rule.Pattern ?? "";
        
        if (string.IsNullOrEmpty(pattern))
            return false;

        var comparison = rule.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return rule.Type switch
        {
            RuleType.Contains => description.Contains(pattern, comparison),
            RuleType.StartsWith => description.StartsWith(pattern, comparison),
            RuleType.EndsWith => description.EndsWith(pattern, comparison),
            RuleType.Equals => description.Equals(pattern, comparison),
            RuleType.Regex => System.Text.RegularExpressions.Regex.IsMatch(description, pattern, 
                rule.IsCaseSensitive ? System.Text.RegularExpressions.RegexOptions.None : 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            _ => false
        };
    }

}

public class UpdateRulePrioritiesCommandHandler : IRequestHandler<UpdateRulePrioritiesCommand, bool>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public UpdateRulePrioritiesCommandHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<bool> Handle(UpdateRulePrioritiesCommand request, CancellationToken cancellationToken)
    {
        await _ruleRepository.UpdateRulePrioritiesAsync(request.UserId, request.RulePriorities, cancellationToken);
        return true;
    }
}

public class CreateRuleFromSuggestionCommandHandler : IRequestHandler<CreateRuleFromSuggestionCommand, CategorizationRuleDto>
{
    private readonly ICategorizationRuleRepository _ruleRepository;

    public CreateRuleFromSuggestionCommandHandler(ICategorizationRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<CategorizationRuleDto> Handle(CreateRuleFromSuggestionCommand request, CancellationToken cancellationToken)
    {
        var rule = new CategorizationRule
        {
            Name = request.Name,
            Pattern = request.Pattern,
            Type = request.Type == "Contains" ? RuleType.Contains : 
                   request.Type == "StartsWith" ? RuleType.StartsWith :
                   request.Type == "EndsWith" ? RuleType.EndsWith :
                   request.Type == "Equals" ? RuleType.Equals :
                   request.Type == "Regex" ? RuleType.Regex : RuleType.Contains,
            CategoryId = request.CategoryId,
            IsActive = request.IsActive,
            Priority = request.Priority,
            IsAiGenerated = true, // Mark as AI-generated since it comes from a suggestion
            IsCaseSensitive = false,
            Logic = RuleLogic.All,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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
            Conditions = new List<RuleConditionDto>(),
            ApplicationCount = 0
        };
    }
}