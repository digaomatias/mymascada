using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using System.Text.Json;

namespace MyMascada.Infrastructure.Services;

public class LlmDescriptionCleaningService : IDescriptionCleaningService
{
    private readonly IUserAiKernelFactory _kernelFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LlmDescriptionCleaningService> _logger;
    private readonly string _systemPrompt;

    private Kernel? _cachedKernel;
    private bool _kernelResolved;

    public LlmDescriptionCleaningService(
        IUserAiKernelFactory kernelFactory,
        ICurrentUserService currentUserService,
        ILogger<LlmDescriptionCleaningService> logger)
    {
        _kernelFactory = kernelFactory;
        _currentUserService = currentUserService;
        _logger = logger;
        _systemPrompt = CreateSystemPrompt();
    }

    private async Task<Kernel?> GetKernelAsync()
    {
        if (!_kernelResolved)
        {
            _cachedKernel = await _kernelFactory.CreateKernelForUserAsync(_currentUserService.GetUserId());
            _kernelResolved = true;
        }
        return _cachedKernel;
    }

    public async Task<DescriptionCleaningResponse> CleanDescriptionsAsync(
        IEnumerable<DescriptionCleaningInput> descriptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var kernel = await GetKernelAsync();
            if (kernel == null)
            {
                return new DescriptionCleaningResponse
                {
                    Success = false,
                    Errors = new List<string> { "AI is not configured. Please configure your AI API key in Settings." }
                };
            }

            var startTime = DateTime.UtcNow;

            var descriptionsList = descriptions.ToList();
            var request = BuildCleaningRequest(descriptionsList);
            var userPrompt = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogDebug("Sending description cleaning request to LLM with {DescriptionCount} descriptions",
                descriptionsList.Count);

            // Add timeout wrapper for additional safety
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(4));

            var response = await kernel.InvokePromptAsync(
                $"{_systemPrompt}\n\nUser Request:\n{userPrompt}",
                cancellationToken: timeoutCts.Token);

            var responseText = response.ToString();

            _logger.LogDebug("Received LLM response length: {Length} characters", responseText.Length);

            var result = ParseLlmResponse(responseText, descriptionsList);
            result.ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM description cleaning timed out after 4 minutes");

            return new DescriptionCleaningResponse
            {
                Success = false,
                Errors = new List<string> { "Description cleaning timed out. Try reducing the number of descriptions or try again later." }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean descriptions using LLM");

            return new DescriptionCleaningResponse
            {
                Success = false,
                Errors = new List<string> { $"LLM description cleaning failed: {ex.Message}" }
            };
        }
    }

    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        var kernel = await GetKernelAsync();
        return kernel != null;
    }

    private static string CreateSystemPrompt()
    {
        return @"You are a financial transaction description cleaning expert. Your task is to transform raw bank transaction descriptions into clean, human-readable text.

**Rules for Cleaning:**
1. Remove transaction codes, reference numbers, and internal bank identifiers
2. Extract and format the merchant/payee name properly (Title Case)
3. Remove redundant location codes (but keep city/country if meaningful)
4. Remove card numbers, terminal IDs, and authorization codes
5. Keep meaningful context like ""Online"", ""Recurring"", ""Refund""
6. If a MerchantNameHint is provided, use it ONLY if it matches the raw description. If they conflict, trust the raw description.
7. Never invent information - only reformat what's there
8. If the description is already clean, return it as-is with high confidence
9. Remove processing terms: VISA, MCARD, EFTPOS, POS, DEBIT, etc.

**Examples:**
- ""POS 4829 COUNTDOWN AUCKLAND NZ 23/01"" -> ""Countdown""
- ""AMZN MKTP US*RT4K92JF0 AMZN.COM/BILLWA"" -> ""Amazon Marketplace""
- ""DIRECT DEBIT VODAFONE NZ 839201"" -> ""Vodafone NZ""
- ""TFR TO 02-1234-5678901-00 REF: RENT JAN"" -> ""Transfer - Rent Jan""
- ""SQ *GOOD COFFEE CO WELLINGTON"" -> ""Good Coffee Co""
- ""PAYPAL *SPOTIFY 402938402"" -> ""Spotify (via PayPal)""
- ""INTEREST EARNED"" -> ""Interest Earned""
- ""Flight Centre Mcard Flight Centr Matias Leote Id1104962764"" -> ""Flight Centre""

**Confidence Levels:**
- High (0.9-1.0): Clear merchant name extraction, unambiguous
- Medium (0.7-0.89): Reasonable interpretation, minor ambiguity
- Low (0.0-0.69): Uncertain, multiple interpretations possible

**Response Format:**
Return ONLY valid JSON, no markdown:
{
  ""success"": true,
  ""results"": [
    {
      ""transactionId"": 123,
      ""originalDescription"": ""POS 4829 COUNTDOWN AUCKLAND NZ"",
      ""description"": ""Countdown"",
      ""confidence"": 0.95,
      ""reasoning"": ""Extracted merchant name 'Countdown' from POS transaction, removed terminal ID and location codes""
    }
  ]
}";
    }

    private static object BuildCleaningRequest(List<DescriptionCleaningInput> descriptions)
    {
        return new
        {
            descriptions = descriptions.Select(d => new
            {
                transactionId = d.TransactionId,
                originalDescription = d.OriginalDescription,
                merchantNameHint = d.MerchantNameHint
            })
        };
    }

    private DescriptionCleaningResponse ParseLlmResponse(
        string responseText,
        List<DescriptionCleaningInput> descriptions)
    {
        try
        {
            // Clean response - remove markdown formatting if present
            var cleanResponse = responseText.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            else if (cleanResponse.StartsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(3);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<LlmCleaningResponseDto>(cleanResponse.Trim(), options);

            if (parsed == null)
            {
                throw new InvalidOperationException("Failed to deserialize LLM response");
            }

            var response = new DescriptionCleaningResponse
            {
                Success = parsed.Success,
                Results = parsed.Results?.Select(r => new CleanedDescription
                {
                    TransactionId = r.TransactionId,
                    OriginalDescription = r.OriginalDescription,
                    Description = r.Description,
                    Confidence = r.Confidence,
                    Reasoning = r.Reasoning
                }).ToList() ?? new List<CleanedDescription>()
            };

            // Validate that all transaction IDs are accounted for
            var inputIds = descriptions.Select(d => d.TransactionId).ToHashSet();
            var responseIds = response.Results.Select(r => r.TransactionId).ToHashSet();
            var missingIds = inputIds.Except(responseIds).ToList();

            if (missingIds.Any())
            {
                _logger.LogWarning(
                    "LLM response missing transaction IDs: {MissingIds}",
                    string.Join(", ", missingIds));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LLM description cleaning response: {Response}", responseText);

            return new DescriptionCleaningResponse
            {
                Success = false,
                Errors = new List<string> { $"Failed to parse LLM response: {ex.Message}" }
            };
        }
    }

    private class LlmCleaningResponseDto
    {
        public bool Success { get; set; }
        public List<LlmCleaningResultDto> Results { get; set; } = new();
    }

    private class LlmCleaningResultDto
    {
        public int TransactionId { get; set; }
        public string OriginalDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }
}
