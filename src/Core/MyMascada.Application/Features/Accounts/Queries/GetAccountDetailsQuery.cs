using AutoMapper;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Accounts.DTOs;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MyMascada.Application.Features.Accounts.Queries;

public class GetAccountDetailsQuery : IRequest<AccountDetailsDto?>
{
    public int AccountId { get; set; }
    public Guid UserId { get; set; }
}

public class GetAccountDetailsQueryHandler : IRequestHandler<GetAccountDetailsQuery, AccountDetailsDto?>
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMapper _mapper;

    public GetAccountDetailsQueryHandler(
        IAccountRepository accountRepository, 
        ITransactionRepository transactionRepository,
        IMapper mapper)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _mapper = mapper;
    }

    public async Task<AccountDetailsDto?> Handle(GetAccountDetailsQuery request, CancellationToken cancellationToken)
    {
        if (request.AccountId <= 0)
            throw new ArgumentException("AccountId must be greater than 0", nameof(request.AccountId));
        
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(request.UserId));

        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
            return null;

        // Get monthly spending data
        var (currentMonth, previousMonth) = await _transactionRepository.GetMonthlySpendingAsync(request.AccountId, request.UserId);
        
        // Get calculated balance
        var calculatedBalance = await _transactionRepository.GetAccountBalanceAsync(request.AccountId, request.UserId);
        
        // Calculate change
        var changeAmount = currentMonth - previousMonth;
        var changePercentage = previousMonth != 0 ? (changeAmount / previousMonth) * 100 : 0;
        
        var trendDirection = changeAmount switch
        {
            > 0 => "up",
            < 0 => "down",
            _ => "neutral"
        };

        // Map to DTO
        var accountDetailsDto = _mapper.Map<AccountDetailsDto>(account);
        accountDetailsDto.CalculatedBalance = calculatedBalance;
        
        // Add monthly spending data
        accountDetailsDto.MonthlySpending = new MonthlySpendingDto
        {
            CurrentMonthSpending = currentMonth,
            PreviousMonthSpending = previousMonth,
            ChangeAmount = changeAmount,
            ChangePercentage = Math.Round(changePercentage, 1),
            TrendDirection = trendDirection,
            MonthName = DateTime.UtcNow.ToString("MMMM", CultureInfo.InvariantCulture),
            Year = DateTime.UtcNow.Year
        };

        return accountDetailsDto;
    }
}