using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Text.Json;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Service for managing mappings between bank-provided categories and user's MyMascada categories.
/// Handles AI-assisted mapping creation and lookup.
/// </summary>
public class BankCategoryMappingService : IBankCategoryMappingService
{
    private readonly IBankCategoryMappingRepository _mappingRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILlmCategorizationService _llmService;
    private readonly ILogger<BankCategoryMappingService> _logger;

    public BankCategoryMappingService(
        IBankCategoryMappingRepository mappingRepository,
        ICategoryRepository categoryRepository,
        ILlmCategorizationService llmService,
        ILogger<BankCategoryMappingService> logger)
    {
        _mappingRepository = mappingRepository;
        _categoryRepository = categoryRepository;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<BankCategoryMapping?> GetMappingAsync(
        string bankCategoryName,
        string providerId,
        Guid userId,
        CancellationToken ct = default)
    {
        var normalizedName = BankCategoryMapping.Normalize(bankCategoryName);
        return await _mappingRepository.GetByBankCategoryAsync(normalizedName, providerId, userId, ct);
    }

    public async Task<IEnumerable<BankCategoryMapping>> GetUserMappingsAsync(
        Guid userId,
        string? providerId = null,
        CancellationToken ct = default)
    {
        return await _mappingRepository.GetByUserIdAsync(userId, providerId, true, ct);
    }

    public async Task<Dictionary<string, BankCategoryMappingResult?>> ResolveCategoriesAsync(
        IEnumerable<string> bankCategories,
        string providerId,
        Guid userId,
        CancellationToken ct = default)
    {
        var categoryList = bankCategories.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var result = new Dictionary<string, BankCategoryMappingResult?>();

        if (!categoryList.Any())
            return result;

        // Normalize all category names
        var normalizedMap = categoryList.ToDictionary(
            c => c,
            c => BankCategoryMapping.Normalize(c));

        // Batch fetch existing mappings
        var existingMappings = await _mappingRepository.GetBatchByBankCategoriesAsync(
            normalizedMap.Values.Distinct(),
            providerId,
            userId,
            ct);

        var mappingDict = existingMappings.ToDictionary(m => m.NormalizedName);

        // Build result dictionary
        foreach (var category in categoryList)
        {
            var normalized = normalizedMap[category];
            if (mappingDict.TryGetValue(normalized, out var mapping))
            {
                result[category] = new BankCategoryMappingResult
                {
                    Mapping = mapping,
                    CategoryId = mapping.CategoryId,
                    CategoryName = mapping.Category?.Name ?? "",
                    ConfidenceScore = mapping.GetEffectiveConfidence(),
                    WasCreatedByAI = false,
                    NewCategoryCreated = false,
                    IsExcluded = mapping.IsExcluded
                };
            }
            else
            {
                result[category] = null;
            }
        }

        return result;
    }

    public async Task<Dictionary<string, BankCategoryMappingResult>> ResolveAndCreateMappingsAsync(
        IEnumerable<string> bankCategories,
        string providerId,
        Guid userId,
        CancellationToken ct = default)
    {
        var categoryList = bankCategories.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var result = new Dictionary<string, BankCategoryMappingResult>();

        if (!categoryList.Any())
            return result;

        _logger.LogInformation("Resolving {Count} bank categories for user {UserId}", categoryList.Count, userId);

        // Get user's existing categories for exact match checking
        var userCategories = (await _categoryRepository.GetByUserIdAsync(userId)).ToList();

        // Build lookup dictionaries for exact matching (case-insensitive)
        var categoryByName = userCategories
            .GroupBy(c => c.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        // Step 1: Check for exact matches with user's existing categories
        var categoriesNeedingMappingLookup = new List<string>();
        foreach (var bankCategory in categoryList)
        {
            var normalizedBankCategory = bankCategory.Trim().ToLowerInvariant();

            if (categoryByName.TryGetValue(normalizedBankCategory, out var exactMatch))
            {
                // Exact match found - use directly without creating a mapping
                _logger.LogDebug("Exact match found for bank category '{BankCategory}' -> Category '{CategoryName}' (ID: {CategoryId})",
                    bankCategory, exactMatch.Name, exactMatch.Id);

                result[bankCategory] = new BankCategoryMappingResult
                {
                    Mapping = null, // No mapping needed for exact matches
                    CategoryId = exactMatch.Id,
                    CategoryName = exactMatch.Name,
                    ConfidenceScore = 1.0m,
                    WasCreatedByAI = false,
                    NewCategoryCreated = false,
                    WasExactMatch = true,
                    IsExcluded = false // Exact matches are never excluded
                };
            }
            else
            {
                categoriesNeedingMappingLookup.Add(bankCategory);
            }
        }

        _logger.LogInformation("Found {ExactMatches} exact matches, {NeedLookup} categories need mapping lookup",
            result.Count, categoriesNeedingMappingLookup.Count);

        // Step 2: For non-exact matches, check existing mappings
        var unmappedCategories = new List<string>();
        if (categoriesNeedingMappingLookup.Any())
        {
            var existingResolutions = await ResolveCategoriesAsync(categoriesNeedingMappingLookup, providerId, userId, ct);

            foreach (var (category, resolution) in existingResolutions)
            {
                if (resolution != null)
                {
                    result[category] = resolution;
                }
                else
                {
                    unmappedCategories.Add(category);
                }
            }
        }

        _logger.LogInformation("Found {MappingMatches} existing mappings, {Unmapped} categories need AI mapping",
            categoriesNeedingMappingLookup.Count - unmappedCategories.Count, unmappedCategories.Count);

        // Step 3: If there are still unmapped categories, use AI to create mappings
        if (unmappedCategories.Any())
        {
            _logger.LogInformation("Creating AI mappings for {Count} unmapped categories", unmappedCategories.Count);

            var aiResponse = await CreateAIMappingsAsync(unmappedCategories, providerId, userId, ct);

            if (aiResponse.Success)
            {
                foreach (var mapping in aiResponse.Mappings)
                {
                    // Find the original category name
                    var originalCategory = unmappedCategories.FirstOrDefault(c =>
                        BankCategoryMapping.Normalize(c) == BankCategoryMapping.Normalize(mapping.Mapping?.BankCategoryName ?? ""));

                    if (!string.IsNullOrEmpty(originalCategory))
                    {
                        result[originalCategory] = mapping;
                    }
                }
            }
            else
            {
                _logger.LogWarning("AI mapping creation failed: {Errors}", string.Join(", ", aiResponse.Errors));

                // For failed AI mappings, add null results so caller knows they weren't resolved
                foreach (var category in unmappedCategories)
                {
                    if (!result.ContainsKey(category))
                    {
                        // Create a placeholder result with no mapping
                        result[category] = new BankCategoryMappingResult
                        {
                            CategoryId = 0,
                            CategoryName = "",
                            ConfidenceScore = 0,
                            WasCreatedByAI = false,
                            NewCategoryCreated = false,
                            IsExcluded = false
                        };
                    }
                }
            }
        }

        _logger.LogInformation("Resolved {ResolvedCount}/{TotalCount} bank categories",
            result.Count(r => r.Value.CategoryId > 0), categoryList.Count);

        return result;
    }

    public async Task<BankCategoryMappingResponse> CreateAIMappingsAsync(
        IEnumerable<string> unmappedCategories,
        string providerId,
        Guid userId,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var categoryList = unmappedCategories.ToList();
        var response = new BankCategoryMappingResponse { Success = true };

        if (!categoryList.Any())
            return response;

        try
        {
            // Get user's existing categories
            var userCategories = (await _categoryRepository.GetByUserIdAsync(userId)).ToList();

            // Build AI prompt
            var prompt = BuildAIMappingPrompt(categoryList, userCategories);

            _logger.LogDebug("Sending bank category mapping request to AI for {Count} categories", categoryList.Count);

            // Call AI
            var aiResponseText = await _llmService.SendPromptAsync(prompt, ct);

            _logger.LogDebug("Received AI response: {Length} characters", aiResponseText.Length);

            // Parse AI response
            var aiMappings = ParseAIResponse(aiResponseText);

            if (aiMappings == null || !aiMappings.Mappings.Any())
            {
                response.Success = false;
                response.Errors.Add("Failed to parse AI response");
                return response;
            }

            // Process each AI mapping result
            foreach (var aiMapping in aiMappings.Mappings)
            {
                try
                {
                    var mappingResult = await ProcessAIMappingResultAsync(
                        aiMapping, providerId, userId, userCategories, ct);

                    if (mappingResult != null)
                    {
                        response.Mappings.Add(mappingResult);

                        if (mappingResult.NewCategoryCreated)
                            response.Summary.NewCategoriesCreated++;
                        else
                            response.Summary.MappedToExisting++;
                    }
                    else
                    {
                        response.Summary.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process AI mapping for bank category: {Category}", aiMapping.BankCategory);
                    response.Summary.Failed++;
                    response.Errors.Add($"Failed to process mapping for '{aiMapping.BankCategory}': {ex.Message}");
                }
            }

            response.Summary.TotalRequested = categoryList.Count;
            response.Summary.ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AI mappings for bank categories");
            response.Success = false;
            response.Errors.Add($"AI mapping creation failed: {ex.Message}");
        }

        return response;
    }

    private async Task<BankCategoryMappingResult?> ProcessAIMappingResultAsync(
        AIBankCategoryMappingResult aiMapping,
        string providerId,
        Guid userId,
        List<Category> userCategories,
        CancellationToken ct)
    {
        int categoryId;
        string categoryName;
        bool newCategoryCreated = false;

        if (aiMapping.Action == "CREATE_NEW")
        {
            var suggestedName = aiMapping.SuggestedName ?? aiMapping.BankCategory;

            // Check if a category with this name already exists (including inactive ones)
            var existingCategory = await _categoryRepository.GetByNameAsync(suggestedName, userId, includeInactive: true);

            if (existingCategory != null)
            {
                if (existingCategory.IsActive)
                {
                    // Use the existing active category instead of creating a duplicate
                    categoryId = existingCategory.Id;
                    categoryName = existingCategory.Name;
                    _logger.LogInformation(
                        "AI suggested CREATE_NEW for '{SuggestedName}' but active category already exists (ID: {CategoryId}), using existing",
                        suggestedName, categoryId);
                }
                else
                {
                    // Category exists but is inactive - don't create, don't reactivate, skip this mapping
                    _logger.LogWarning(
                        "AI suggested CREATE_NEW for '{SuggestedName}' but inactive category exists (ID: {CategoryId}), skipping mapping",
                        suggestedName, existingCategory.Id);
                    return null;
                }
            }
            else
            {
                // No existing category - create new one
                var newCategory = new Category
                {
                    Name = suggestedName,
                    UserId = userId,
                    ParentCategoryId = aiMapping.SuggestedParentId,
                    Type = CategoryType.Expense, // Default to expense
                    IsActive = true,
                    IsSystemCategory = false
                };

                var createdCategory = await _categoryRepository.AddAsync(newCategory);
                categoryId = createdCategory.Id;
                categoryName = createdCategory.Name;
                newCategoryCreated = true;

                _logger.LogInformation("Created new category '{CategoryName}' (ID: {CategoryId}) for bank category '{BankCategory}'",
                    categoryName, categoryId, aiMapping.BankCategory);
            }
        }
        else if (aiMapping.MappedCategoryId.HasValue)
        {
            categoryId = aiMapping.MappedCategoryId.Value;
            var existingCategory = userCategories.FirstOrDefault(c => c.Id == categoryId);
            categoryName = existingCategory?.Name ?? "";

            if (existingCategory == null)
            {
                _logger.LogWarning("AI suggested category ID {CategoryId} but it doesn't exist", categoryId);
                return null;
            }
        }
        else
        {
            _logger.LogWarning("AI mapping for '{BankCategory}' has no valid action or category ID", aiMapping.BankCategory);
            return null;
        }

        // Create the mapping
        var mapping = new BankCategoryMapping
        {
            BankCategoryName = aiMapping.BankCategory,
            NormalizedName = BankCategoryMapping.Normalize(aiMapping.BankCategory),
            ProviderId = providerId,
            UserId = userId,
            CategoryId = categoryId,
            ConfidenceScore = aiMapping.Confidence,
            Source = "AI",
            IsActive = true
        };

        var savedMapping = await _mappingRepository.AddAsync(mapping, ct);

        return new BankCategoryMappingResult
        {
            Mapping = savedMapping,
            CategoryId = categoryId,
            CategoryName = categoryName,
            ConfidenceScore = aiMapping.Confidence,
            WasCreatedByAI = true,
            NewCategoryCreated = newCategoryCreated,
            IsExcluded = false // Newly created AI mappings are not excluded by default
        };
    }

    private string BuildAIMappingPrompt(List<string> bankCategories, List<Category> userCategories)
    {
        var categoryInfo = userCategories.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            fullPath = c.GetFullPath(),
            type = c.Type.ToString(),
            parentId = c.ParentCategoryId
        }).ToList();

        var prompt = $@"You are a financial category mapping expert. Map the following bank-provided categories to the user's existing categories.

## User's Existing Categories:
{JsonSerializer.Serialize(categoryInfo, new JsonSerializerOptions { WriteIndented = true })}

## Bank Categories to Map:
{JsonSerializer.Serialize(bankCategories, new JsonSerializerOptions { WriteIndented = true })}

## Instructions:
1. For each bank category, determine if it semantically matches an existing user category
2. Map to the MOST SPECIFIC subcategory when available (e.g., ""GROCERIES"" should map to ""Food > Groceries"" not just ""Food"")
3. If NO good match exists, use action ""CREATE_NEW"" with a suggested category name and parent ID
4. Consider semantic similarity, not just exact text matches
5. It's OK to create new categories - the user wants comprehensive coverage

## Response Format (JSON only, no markdown):
{{
  ""success"": true,
  ""mappings"": [
    {{
      ""bankCategory"": ""<original bank category name>"",
      ""action"": ""MAP"" or ""CREATE_NEW"",
      ""mappedCategoryId"": <category ID if action is MAP, null otherwise>,
      ""suggestedName"": ""<suggested name if CREATE_NEW, null otherwise>"",
      ""suggestedParentId"": <parent category ID if CREATE_NEW and parent exists, null otherwise>,
      ""confidence"": <0.0 to 1.0>,
      ""reasoning"": ""<brief explanation>""
    }}
  ]
}}

Respond with ONLY the JSON object, no additional text or markdown formatting.";

        return prompt;
    }

    private AIBankCategoryMappingResponse? ParseAIResponse(string responseText)
    {
        try
        {
            // Strip markdown if present
            var cleanedResponse = responseText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<AIBankCategoryMappingResponse>(cleanedResponse, options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI response as JSON. Response: {Response}", responseText);
            return null;
        }
    }

    public async Task RecordMappingApplicationAsync(int mappingId, CancellationToken ct = default)
    {
        await _mappingRepository.IncrementApplicationCountAsync(mappingId, ct);
    }

    public async Task RecordMappingOverrideAsync(
        int mappingId,
        int newCategoryId,
        bool createNewMapping,
        CancellationToken ct = default)
    {
        await _mappingRepository.IncrementOverrideCountAsync(mappingId, ct);

        if (createNewMapping)
        {
            var existingMapping = await _mappingRepository.GetByIdAsync(mappingId, ct);
            if (existingMapping != null)
            {
                // Create a learned mapping with the corrected category
                var learnedMapping = new BankCategoryMapping
                {
                    BankCategoryName = existingMapping.BankCategoryName,
                    NormalizedName = existingMapping.NormalizedName,
                    ProviderId = existingMapping.ProviderId,
                    UserId = existingMapping.UserId,
                    CategoryId = newCategoryId,
                    ConfidenceScore = 0.7m, // Lower confidence for learned mappings
                    Source = "Learned",
                    IsActive = true
                };

                // Deactivate old mapping
                existingMapping.IsActive = false;
                await _mappingRepository.UpdateAsync(existingMapping, ct);

                // Add new mapping
                await _mappingRepository.AddAsync(learnedMapping, ct);
            }
        }
    }

    public async Task<BankCategoryMapping> UpsertMappingAsync(
        string bankCategoryName,
        string providerId,
        int categoryId,
        Guid userId,
        CancellationToken ct = default)
    {
        var normalizedName = BankCategoryMapping.Normalize(bankCategoryName);

        var existingMapping = await _mappingRepository.GetByBankCategoryAsync(
            normalizedName, providerId, userId, ct);

        if (existingMapping != null)
        {
            existingMapping.CategoryId = categoryId;
            existingMapping.ConfidenceScore = 1.0m; // User-confirmed = highest confidence
            existingMapping.Source = "User";
            existingMapping.UpdatedAt = DateTime.UtcNow;
            await _mappingRepository.UpdateAsync(existingMapping, ct);
            return existingMapping;
        }
        else
        {
            var newMapping = new BankCategoryMapping
            {
                BankCategoryName = bankCategoryName,
                NormalizedName = normalizedName,
                ProviderId = providerId,
                UserId = userId,
                CategoryId = categoryId,
                ConfidenceScore = 1.0m,
                Source = "User",
                IsActive = true
            };

            return await _mappingRepository.AddAsync(newMapping, ct);
        }
    }

    public async Task<bool> DeleteMappingAsync(int mappingId, Guid userId, CancellationToken ct = default)
    {
        var mapping = await _mappingRepository.GetByIdAsync(mappingId, userId, ct);
        if (mapping == null)
            return false;

        await _mappingRepository.DeleteAsync(mapping, ct);
        return true;
    }

    public async Task<BankCategoryMapping?> GetByIdAsync(int mappingId, Guid userId, CancellationToken ct = default)
    {
        return await _mappingRepository.GetByIdAsync(mappingId, userId, ct);
    }
}
