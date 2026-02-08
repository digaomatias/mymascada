using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Accounts.DTOs;
using MyMascada.Application.Features.Accounts.Queries;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccountAccessService _accountAccess;
    private readonly IFeatureFlags _featureFlags;
    private readonly IAccountShareRepository _accountShareRepository;

    public AccountsController(
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IMapper mapper,
        IMediator mediator,
        ICurrentUserService currentUserService,
        IAccountAccessService accountAccess,
        IFeatureFlags featureFlags,
        IAccountShareRepository accountShareRepository)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _mapper = mapper;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _accountAccess = accountAccess;
        _featureFlags = featureFlags;
        _accountShareRepository = accountShareRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAccounts()
    {
        var userId = _currentUserService.GetUserId();
        var accounts = await _accountRepository.GetByUserIdAsync(userId);
        var accountDtos = _mapper.Map<List<AccountDto>>(accounts);

        if (_featureFlags.AccountSharing)
            await PopulateSharingMetadataAsync(accountDtos, userId);

        return Ok(accountDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDto>> GetAccount(int id)
    {
        var userId = _currentUserService.GetUserId();
        var account = await _accountRepository.GetByIdAsync(id, userId);
        if (account == null)
        {
            return NotFound();
        }

        var accountDto = _mapper.Map<AccountDto>(account);

        if (_featureFlags.AccountSharing)
            await PopulateSharingMetadataAsync(new List<AccountDto> { accountDto }, userId);

        return Ok(accountDto);
    }

    [HttpGet("{id}/details")]
    public async Task<ActionResult<AccountDetailsDto>> GetAccountDetails(int id)
    {
        var query = new GetAccountDetailsQuery 
        { 
            AccountId = id, 
            UserId = _currentUserService.GetUserId() 
        };
        
        var accountDetails = await _mediator.Send(query);
        if (accountDetails == null)
        {
            return NotFound();
        }
        
        return Ok(accountDetails);
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> CreateAccount([FromBody] CreateAccountDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var account = _mapper.Map<Account>(request);
        account.UserId = _currentUserService.GetUserId();
        account.CreatedAt = DateTimeProvider.UtcNow;
        account.UpdatedAt = DateTimeProvider.UtcNow;

        var createdAccount = await _accountRepository.AddAsync(account);
        var accountDto = _mapper.Map<AccountDto>(createdAccount);
        
        return CreatedAtAction(nameof(GetAccount), new { id = createdAccount.Id }, accountDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AccountDto>> UpdateAccount(int id, [FromBody] UpdateAccountDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id != request.Id)
        {
            return BadRequest("ID in URL does not match ID in request body");
        }

        var userId = _currentUserService.GetUserId();

        // Only the owner can update account settings
        if (!await _accountAccess.IsOwnerAsync(userId, id))
            return NotFound();

        var existingAccount = await _accountRepository.GetByIdAsync(id, userId);
        if (existingAccount == null)
        {
            return NotFound();
        }

        // Update properties using AutoMapper
        _mapper.Map(request, existingAccount);
        existingAccount.UpdatedAt = DateTimeProvider.UtcNow;

        await _accountRepository.UpdateAsync(existingAccount);

        var accountDto = _mapper.Map<AccountDto>(existingAccount);
        return Ok(accountDto);
    }

    [HttpPatch("{id}/archive")]
    public async Task<ActionResult> ArchiveAccount(int id)
    {
        var userId = _currentUserService.GetUserId();

        // Only the owner can archive an account
        if (!await _accountAccess.IsOwnerAsync(userId, id))
            return NotFound();

        var account = await _accountRepository.GetByIdAsync(id, userId);
        if (account == null)
        {
            return NotFound();
        }

        // Check if account has transactions
        var hasTransactions = (await _transactionRepository.GetByAccountIdAsync(id, userId)).Any();
        if (hasTransactions)
        {
            return BadRequest(new
            {
                message = "Cannot archive account with transactions",
                details = "Transaction history must be preserved for data integrity"
            });
        }

        // Revoke all active shares for this account
        if (_featureFlags.AccountSharing)
            await _accountShareRepository.RevokeSharesByAccountIdAsync(id);

        // Perform soft delete using the standard deletion method
        await _accountRepository.DeleteAsync(account);
        return NoContent();
    }

    [HttpGet("{id}/transactions")]
    public async Task<ActionResult<bool>> HasTransactions(int id)
    {
        var account = await _accountRepository.GetByIdAsync(id, _currentUserService.GetUserId());
        if (account == null)
        {
            return NotFound();
        }

        var hasTransactions = (await _transactionRepository.GetByAccountIdAsync(id, _currentUserService.GetUserId())).Any();
        return Ok(new { hasTransactions });
    }

    [HttpGet("with-balances")]
    public async Task<ActionResult<IEnumerable<AccountWithBalanceDto>>> GetAccountsWithBalances()
    {
        var userId = _currentUserService.GetUserId();
        var accounts = await _accountRepository.GetByUserIdAsync(userId);

        // Get all account balances in a single query (optimized!)
        var accountBalances = await _transactionRepository.GetAccountBalancesAsync(userId);

        var accountsWithBalances = accounts.Select(account => {
            var dto = _mapper.Map<AccountWithBalanceDto>(account);
            dto.CalculatedBalance = accountBalances.GetValueOrDefault(account.Id, 0m);
            return dto;
        }).ToList();

        if (_featureFlags.AccountSharing)
            await PopulateSharingMetadataAsync(accountsWithBalances, userId);

        return Ok(accountsWithBalances);
    }

    [HttpGet("{id}/with-balance")]
    public async Task<ActionResult<AccountWithBalanceDto>> GetAccountWithBalance(int id)
    {
        var userId = _currentUserService.GetUserId();
        var account = await _accountRepository.GetByIdAsync(id, userId);

        if (account == null)
        {
            return NotFound();
        }

        // Get calculated balance using the repository method that includes initial balance
        var calculatedBalance = await _transactionRepository.GetAccountBalanceAsync(id, userId);

        var accountWithBalance = _mapper.Map<AccountWithBalanceDto>(account);
        accountWithBalance.CalculatedBalance = calculatedBalance;

        if (_featureFlags.AccountSharing)
            await PopulateSharingMetadataAsync(new List<AccountWithBalanceDto> { accountWithBalance }, userId);

        return Ok(accountWithBalance);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAccount(int id)
    {
        var userId = _currentUserService.GetUserId();

        // Only the owner can delete an account
        if (!await _accountAccess.IsOwnerAsync(userId, id))
            return NotFound();

        var account = await _accountRepository.GetByIdAsync(id, userId);

        if (account == null)
        {
            return NotFound();
        }

        // Revoke all active shares for this account
        if (_featureFlags.AccountSharing)
            await _accountShareRepository.RevokeSharesByAccountIdAsync(id);

        // Delete all transactions associated with this account first
        await _transactionRepository.DeleteByAccountIdAsync(id, userId);

        // Then delete the account
        await _accountRepository.DeleteAsync(account);

        return NoContent();
    }

    /// <summary>
    /// Populates sharing metadata (IsOwner, IsSharedWithMe, ShareRole, SharedByUserName)
    /// on a list of DTOs that implement ISharingMetadata when AccountSharing feature is enabled.
    /// </summary>
    private async Task PopulateSharingMetadataAsync<T>(List<T> dtos, Guid viewerUserId)
        where T : ISharingMetadata
    {
        var receivedShares = (await _accountShareRepository.GetAcceptedSharesForUserAsync(viewerUserId))
            .ToDictionary(s => s.AccountId);

        foreach (var dto in dtos)
        {
            if (receivedShares.TryGetValue(dto.Id, out var share))
            {
                dto.IsOwner = false;
                dto.IsSharedWithMe = true;
                dto.ShareRole = share.Role;
                dto.SharedByUserName = share.SharedByUser?.FullName;
            }
            else
            {
                dto.IsOwner = true;
                dto.IsSharedWithMe = false;
            }
        }
    }
}