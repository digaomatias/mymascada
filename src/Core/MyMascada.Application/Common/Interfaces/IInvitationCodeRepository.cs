using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface IInvitationCodeRepository
{
    Task AddAsync(InvitationCode code);
    Task<InvitationCode?> GetByNormalizedCodeAsync(string normalizedCode);
    Task<InvitationCode?> GetByIdAsync(Guid id);
    Task<(IReadOnlyList<InvitationCode> Items, int TotalCount)> GetPagedAsync(InvitationCodeStatus? status, int page, int pageSize);
    Task RevokeActiveCodesForEntryAsync(Guid waitlistEntryId);
    Task UpdateAsync(InvitationCode code);
}
