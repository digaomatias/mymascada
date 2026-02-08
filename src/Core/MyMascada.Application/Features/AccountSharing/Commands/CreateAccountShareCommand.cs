using System.Security.Cryptography;
using System.Text;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AccountSharing.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Commands;

/// <summary>
/// Command for an account owner to invite a user by email to share their account.
/// </summary>
public class CreateAccountShareCommand : IRequest<CreateAccountShareResult>
{
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public AccountShareRole Role { get; set; }
}

/// <summary>
/// Result of creating an account share, including the raw invitation token for the link.
/// </summary>
public class CreateAccountShareResult
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class CreateAccountShareCommandHandler : IRequestHandler<CreateAccountShareCommand, CreateAccountShareResult>
{
    private readonly IAccountShareRepository _accountShareRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IUserRepository _userRepository;

    public CreateAccountShareCommandHandler(
        IAccountShareRepository accountShareRepository,
        IAccountAccessService accountAccessService,
        IUserRepository userRepository)
    {
        _accountShareRepository = accountShareRepository;
        _accountAccessService = accountAccessService;
        _userRepository = userRepository;
    }

    public async Task<CreateAccountShareResult> Handle(CreateAccountShareCommand request, CancellationToken cancellationToken)
    {
        // Validate the requesting user is the account owner
        var isOwner = await _accountAccessService.IsOwnerAsync(request.UserId, request.AccountId);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the account owner can share this account.");

        // Rate limit: max 10 pending invitations per account
        var pendingCount = await _accountShareRepository.GetPendingCountForAccountAsync(request.AccountId);
        if (pendingCount >= 10)
            throw new InvalidOperationException("Maximum of 10 pending invitations per account has been reached.");

        // Look up the target user by email
        var targetUser = await _userRepository.GetByEmailAsync(request.Email);
        if (targetUser == null)
            throw new ArgumentException("Unable to create share invitation. Please verify the email address and try again.");

        // Prevent sharing with yourself
        if (targetUser.Id == request.UserId)
            throw new InvalidOperationException("You cannot share an account with yourself.");

        // Check if there is already an active share (Pending or Accepted) for this user + account
        var existingShare = await _accountShareRepository.GetActiveShareAsync(request.AccountId, targetUser.Id);
        if (existingShare != null)
            throw new InvalidOperationException("An active share already exists for this user on this account.");

        // Generate invitation token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLower();

        // Create the AccountShare entity
        var share = new AccountShare
        {
            AccountId = request.AccountId,
            SharedWithUserId = targetUser.Id,
            SharedByUserId = request.UserId,
            Role = request.Role,
            Status = AccountShareStatus.Pending,
            InvitationToken = tokenHash,
            InvitationExpiresAt = DateTimeProvider.UtcNow.AddDays(7)
        };

        try
        {
            var createdShare = await _accountShareRepository.AddAsync(share);

            return new CreateAccountShareResult
            {
                Id = createdShare.Id,
                Token = rawToken
            };
        }
        catch (Exception ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true
                                   || ex.InnerException?.Message?.Contains("unique constraint") == true)
        {
            // Unique index violation from concurrent duplicate share creation
            throw new InvalidOperationException("An active share already exists for this user on this account.");
        }
    }
}
