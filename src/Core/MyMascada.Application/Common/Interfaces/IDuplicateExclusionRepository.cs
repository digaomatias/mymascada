using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IDuplicateExclusionRepository
{
    /// <summary>
    /// Gets all duplicate exclusions for a user
    /// </summary>
    Task<List<DuplicateExclusion>> GetByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Checks if a specific set of transaction IDs has been excluded by the user
    /// </summary>
    Task<bool> IsExcludedAsync(Guid userId, IEnumerable<int> transactionIds);
    
    /// <summary>
    /// Adds a new duplicate exclusion
    /// </summary>
    Task<DuplicateExclusion> AddAsync(DuplicateExclusion exclusion);
    
    /// <summary>
    /// Gets exclusions that apply to any of the given transaction IDs
    /// </summary>
    Task<List<DuplicateExclusion>> GetApplicableExclusionsAsync(Guid userId, IEnumerable<int> transactionIds);
}