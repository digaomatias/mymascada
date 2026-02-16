using AutoMapper;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Services;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.Transactions.Commands;

public class CreateTransactionCommand : IRequest<TransactionDto>
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? Notes { get; set; }
    public string? Location { get; set; }
    public string? Tags { get; set; }
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string? IdempotencyToken { get; set; }
    public bool AllowDuplicates { get; set; } = false;
}

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IMapper _mapper;
    private readonly TransactionDuplicateChecker _duplicateChecker;
    private readonly ICategorizationPipeline _categorizationPipeline;

    public CreateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IAccountAccessService accountAccessService,
        IMapper mapper,
        TransactionDuplicateChecker duplicateChecker,
        ICategorizationPipeline categorizationPipeline)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _accountAccessService = accountAccessService;
        _mapper = mapper;
        _duplicateChecker = duplicateChecker;
        _categorizationPipeline = categorizationPipeline;
    }

    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        // Validate account exists and belongs to user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
        {
            throw new ArgumentException($"Account with ID {request.AccountId} not found or does not belong to user");
        }

        // Verify the user has modify permission on this account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, request.AccountId))
        {
            throw new UnauthorizedAccessException("You do not have permission to create transactions on this account.");
        }

        // Validate category if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId.Value, request.UserId);
            if (!categoryExists)
            {
                throw new ArgumentException($"Category with ID {request.CategoryId} not found or does not belong to user");
            }
        }

        // Check for duplicates unless explicitly allowed
        if (!request.AllowDuplicates)
        {
            var existingTransaction = await _duplicateChecker.CheckForDuplicatesAsync(request);
            if (existingTransaction != null)
            {
                return _mapper.Map<TransactionDto>(existingTransaction);
            }
        }

        // Create transaction  
        string? externalId = request.AllowDuplicates ? null : _duplicateChecker.GenerateTransactionHash(request);
        var transaction = new Transaction
        {
            Amount = request.Amount,
            TransactionDate = DateTimeProvider.ToUtc(request.TransactionDate),
            Description = request.Description,
            UserDescription = request.UserDescription,
            Status = request.Status,
            Source = TransactionSource.Manual,
            ExternalId = externalId,
            Notes = request.Notes,
            Location = request.Location,
            Tags = request.Tags,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            IsReviewed = request.CategoryId.HasValue, // User explicitly chose a category — no need to review
            IsExcluded = false,
            CreatedAt = DateTimeProvider.UtcNow,
            UpdatedAt = DateTimeProvider.UtcNow
        };

        var createdTransaction = await _transactionRepository.AddAsync(transaction);

        // Apply automatic categorization if no category was provided
        if (!request.CategoryId.HasValue)
        {
            var result = await _categorizationPipeline.ProcessAsync(new[] { createdTransaction }, cancellationToken);
            if (result.CategorizedTransactions.Any())
            {
                var categorizedTransaction = result.CategorizedTransactions.First();
                createdTransaction.CategoryId = categorizedTransaction.CategoryId;
                await _transactionRepository.UpdateAsync(createdTransaction);
            }
        }

        // Return DTO
        return _mapper.Map<TransactionDto>(createdTransaction);
    }
}