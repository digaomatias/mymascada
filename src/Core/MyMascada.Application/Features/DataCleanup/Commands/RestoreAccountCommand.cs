using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.DataCleanup.DTOs;

namespace MyMascada.Application.Features.DataCleanup.Commands;

/// <summary>
/// Command to restore a soft-deleted account
/// </summary>
public class RestoreAccountCommand : IRequest<CleanupOperationResult>
{
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
    public bool RestoreAsActive { get; set; } = true;
}

/// <summary>
/// Handler for restoring soft-deleted accounts
/// </summary>
public class RestoreAccountCommandHandler : IRequestHandler<RestoreAccountCommand, CleanupOperationResult>
{
    private readonly IAccountRepository _accountRepository;

    public RestoreAccountCommandHandler(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<CleanupOperationResult> Handle(RestoreAccountCommand request, CancellationToken cancellationToken)
    {
        var result = new CleanupOperationResult();

        try
        {
            // Check if the account exists and is soft-deleted
            var account = await _accountRepository.GetByIdIncludingDeletedAsync(request.AccountId, request.UserId);
            
            if (account == null)
            {
                result.Success = false;
                result.Message = "Account not found";
                result.Errors.Add("The specified account does not exist or does not belong to the user");
                return result;
            }

            if (!account.IsDeleted)
            {
                result.Success = false;
                result.Message = "Account is not deleted";
                result.Errors.Add("The specified account is not in a deleted state");
                return result;
            }

            // Restore the account
            await _accountRepository.RestoreAccountAsync(request.AccountId, request.UserId);

            if (request.RestoreAsActive)
            {
                account.IsActive = true;
                await _accountRepository.UpdateAsync(account);
            }

            result.Success = true;
            result.Message = $"Account '{account.Name}' has been successfully restored";
            result.ProcessedCount = 1;
            result.Details["AccountName"] = account.Name;
            result.Details["RestoredAsActive"] = request.RestoreAsActive;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "An error occurred while restoring the account";
            result.Errors.Add(ex.Message);
        }

        return result;
    }
}