using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to check if a user has Akahu credentials configured.
/// Safe to expose via API - only returns boolean, no sensitive data.
/// </summary>
public record HasAkahuCredentialsQuery(Guid UserId) : IRequest<bool>;

/// <summary>
/// Handler for checking if user has Akahu credentials.
/// </summary>
public class HasAkahuCredentialsQueryHandler : IRequestHandler<HasAkahuCredentialsQuery, bool>
{
    private readonly IAkahuUserCredentialRepository _credentialRepository;

    public HasAkahuCredentialsQueryHandler(IAkahuUserCredentialRepository credentialRepository)
    {
        _credentialRepository = credentialRepository;
    }

    public async Task<bool> Handle(HasAkahuCredentialsQuery request, CancellationToken cancellationToken)
    {
        return await _credentialRepository.HasCredentialsAsync(request.UserId, cancellationToken);
    }
}
