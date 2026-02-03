using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Net.Http;
using System.Text.Json;

namespace MyMascada.Infrastructure.Services;

public class LlmCategorizationService : ILlmCategorizationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmCategorizationService> _logger;
    private readonly Kernel _kernel;
    private readonly string _systemPrompt;

    public LlmCategorizationService(
        IConfiguration configuration,
        ILogger<LlmCategorizationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _kernel = CreateKernel();
        _systemPrompt = CreateSystemPrompt();
    }

    public async Task<LlmCategorizationResponse> CategorizeTransactionsAsync(
        IEnumerable<Transaction> transactions,
        IEnumerable<Category> categories,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            var request = BuildCategorizationRequest(transactions, categories);
            var userPrompt = JsonSerializer.Serialize(request, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true 
            });

            _logger.LogDebug("Sending categorization request to LLM with {TransactionCount} transactions: {TransactionIds}",
                transactions.Count(), string.Join(", ", transactions.Select(t => t.Id)));

            // Add timeout wrapper for additional safety
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(4)); // Slightly less than HTTP timeout

            var response = await _kernel.InvokePromptAsync($"{_systemPrompt}\n\nUser Request:\n{userPrompt}", 
                cancellationToken: timeoutCts.Token);

            var responseText = response.ToString();
            
            _logger.LogDebug("Received LLM response length: {Length} characters", responseText.Length);
            _logger.LogDebug("LLM response preview: {Preview}...", responseText.Length > 500 ? responseText.Substring(0, 500) : responseText);

            var result = ParseLlmResponse(responseText, transactions);
            result.Summary.ProcessingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return result;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LLM categorization timed out for {TransactionCount} transactions after 4 minutes", 
                transactions.Count());
            
            return new LlmCategorizationResponse
            {
                Success = false,
                Errors = new List<string> { "LLM categorization timed out. Try reducing the number of transactions or try again later." }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to categorize transactions using LLM");
            
            return new LlmCategorizationResponse
            {
                Success = false,
                Errors = new List<string> { $"LLM categorization failed: {ex.Message}" }
            };
        }
    }

    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple health check
            var response = await _kernel.InvokePromptAsync("Reply with 'OK' if you're available.", 
                cancellationToken: cancellationToken);
            
            return response.ToString().Contains("OK", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM service health check failed");
            return false;
        }
    }

    public async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send prompt to LLM");
            throw;
        }
    }

    private Kernel CreateKernel()
    {
        var apiKey = _configuration["LLM:OpenAI:ApiKey"];
        var model = _configuration["LLM:OpenAI:Model"] ?? "gpt-4o-mini";

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured");
        }

        var builder = Kernel.CreateBuilder();
        
        // Configure OpenAI with extended timeout for batch processing
        builder.AddOpenAIChatCompletion(model, apiKey, httpClient: CreateHttpClientWithTimeout());
        
        return builder.Build();
    }

    private HttpClient CreateHttpClientWithTimeout()
    {
        var httpClient = new HttpClient();
        
        // Set timeout to 5 minutes for LLM batch processing
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        
        return httpClient;
    }

    private string CreateSystemPrompt()
    {
        return @"You are a financial transaction categorization expert. Your task is to analyze financial transactions and suggest appropriate categories based on:

1. Transaction descriptions (merchant names, transaction types)
2. Transaction amounts and income/expense type (typical spending patterns, salary identification)
3. Transaction type (Income vs Expense) - use this to guide category selection
4. Existing categorization rules
5. Available categories
6. Currency context (regional merchant identification)

**Transaction Type Analysis:**
- **Income Transactions (amount > 0)**: Look for salary, freelance, investment returns, refunds, transfers
  - Regular amounts (e.g., $3000-5000) on specific dates = likely salary
  - Irregular larger amounts = bonuses, freelance payments, investment gains
  - Small amounts from merchants = refunds, cashback
- **Expense Transactions (amount < 0)**: Standard spending categorization
  - Use amount size as context (e.g., $2000+ might be rent, $50-200 groceries, $5-20 coffee/snacks)

**Currency-Based Merchant Recognition:**
- **NZD (New Zealand)**: New World, Pak'nSave, Countdown, Mitre 10 = groceries/retail
- **AUD (Australia)**: Woolworths, Coles, Bunnings = groceries/hardware
- **USD (United States)**: Walmart, Target, Safeway = department/grocery stores
- **GBP (United Kingdom)**: Tesco, ASDA, Boots = groceries/pharmacy
- **EUR (Europe)**: Carrefour, Metro = groceries/retail

**Instructions:**
- Return ONLY valid JSON in the exact format specified
- USE TRANSACTION TYPE (Income/Expense) as primary filter - only suggest Income categories for income transactions
- USE AMOUNT SIZE as context clue (large regular amounts = salary, small amounts = coffee/snacks, etc.)
- USE CURRENCY to identify regional merchants accurately
- Provide confidence scores between 0.0 and 1.0
- Include reasoning for each suggestion that mentions transaction type and amount context
- Be conservative with confidence scores (prefer accuracy over confidence)
- Focus on pattern recognition and semantic understanding of transaction descriptions
- ALWAYS provide exactly 3 category suggestions per transaction (ordered by confidence)
- Include the top recommendation plus 2 alternatives even if confidence is lower
- CRITICAL: Each transaction MUST have exactly 3 suggestions in the suggestions array

**Response Format:**
```json
{
  ""success"": true,
  ""categorizations"": [
    {
      ""transactionId"": 123,
      ""suggestions"": [
        {
          ""categoryId"": 15,
          ""categoryName"": ""Online Shopping"",
          ""confidence"": 0.92,
          ""reasoning"": ""Expense transaction from Amazon.com ($89.99) - typical online shopping amount, clearly e-commerce purchase"",
          ""matchingRules"": []
        },
        {
          ""categoryId"": 8,
          ""categoryName"": ""Electronics"",
          ""confidence"": 0.75,
          ""reasoning"": ""Amazon expense of $89.99 suggests electronics purchase - common price range for gadgets"",
          ""matchingRules"": []
        },
        {
          ""categoryId"": 12,
          ""categoryName"": ""Books & Media"",
          ""confidence"": 0.65,
          ""reasoning"": ""Amazon expense, though $89.99 seems high for books - could be multiple items or textbooks"",
          ""matchingRules"": []
        }
      ],
      ""recommendedCategoryId"": 15,
      ""requiresReview"": false,
      ""suggestedRule"": null
    }
  ],
  ""summary"": {
    ""totalProcessed"": 1,
    ""highConfidence"": 1,
    ""mediumConfidence"": 0,
    ""lowConfidence"": 0,
    ""averageConfidence"": 0.77,
    ""newRulesGenerated"": 0
  }
}
```

**Income Transaction Example:**
For a $3500 income from ""Acme Corp Payroll"":
- Primary: ""Salary"" (95% confidence) - ""Regular income amount from employer, matches typical salary pattern""
- Secondary: ""Freelance Income"" (40% confidence) - ""Could be contractor payment, though amount suggests regular employment""
- Tertiary: ""Bonus"" (30% confidence) - ""Possible bonus payment, though timing would need verification""

**Confidence Levels:**
- High (0.8-1.0): Very certain about the categorization
- Medium (0.6-0.79): Reasonable confidence, but could be wrong
- Low (0.0-0.59): Uncertain, requires human review

Note: Rule generation and matching is handled by the Rules Handler in the categorization pipeline. Focus on semantic understanding and pattern recognition for categorization suggestions.";
    }

    private object BuildCategorizationRequest(
        IEnumerable<Transaction> transactions,
        IEnumerable<Category> categories)
    {
        return new
        {
            transactions = transactions.Select(t => new
            {
                id = t.Id,
                amount = t.Amount,
                absoluteAmount = Math.Abs(t.Amount),
                isIncome = t.Amount > 0,
                transactionType = t.Amount > 0 ? "Income" : "Expense",
                description = t.Description,
                userDescription = t.UserDescription,
                transactionDate = t.TransactionDate,
                accountId = t.AccountId,
                accountName = t.Account?.Name,
                currency = t.Account?.Currency ?? "USD",
                currentCategoryId = t.CategoryId,
                tags = t.Tags,
                location = t.Location
            }),
            availableCategories = categories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                fullPath = GetCategoryPath(c, categories),
                type = c.Type.ToString(),
                parentId = c.ParentCategoryId,
                color = c.Color,
                keywords = ExtractKeywords(c.Name)
            }),
            // Note: Rules processing is now handled by the RulesHandler in the categorization pipeline
            // The LLM service is only responsible for LLM-based categorization
            context = new
            {
                batchSize = transactions.Count(),
                language = "en",
                currencies = transactions.Select(t => t.Account?.Currency ?? "USD").Distinct().ToList(),
                instructions = "Use currency information to identify regional merchants. For example: NZD transactions with 'New World' should be categorized as groceries, not dining."
            }
        };
    }

    private string GetCategoryPath(Category category, IEnumerable<Category> allCategories)
    {
        var path = new List<string> { category.Name };
        var current = category;

        while (current.ParentCategoryId.HasValue)
        {
            var parent = allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId);
            if (parent == null) break;
            
            path.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" > ", path);
    }

    private List<string> ExtractKeywords(string categoryName)
    {
        return categoryName.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();
    }

    private LlmCategorizationResponse ParseLlmResponse(string responseText, IEnumerable<Transaction> transactions)
    {
        try
        {
            // Clean response - remove markdown formatting if present
            var cleanResponse = responseText.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
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

            var response = JsonSerializer.Deserialize<LlmCategorizationResponse>(cleanResponse, options);
            
            if (response == null)
            {
                throw new InvalidOperationException("Failed to deserialize LLM response");
            }

            // Validate response
            ValidateResponse(response, transactions);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response: {Response}", responseText);
            
            return new LlmCategorizationResponse
            {
                Success = false,
                Errors = new List<string> { $"Failed to parse LLM response: {ex.Message}" }
            };
        }
    }

    private void ValidateResponse(LlmCategorizationResponse response, IEnumerable<Transaction> transactions)
    {
        var transactionIds = transactions.Select(t => t.Id).ToHashSet();
        var invalidCategorizations = response.Categorizations
            .Where(c => !transactionIds.Contains(c.TransactionId))
            .ToList();

        if (invalidCategorizations.Any())
        {
            _logger.LogWarning("LLM response contains invalid transaction IDs: {InvalidIds}",
                string.Join(", ", invalidCategorizations.Select(c => c.TransactionId)));
        }

        // Calculate summary if not provided
        if (response.Summary.TotalProcessed == 0 && response.Categorizations.Any())
        {
            response.Summary = new CategorizationSummary
            {
                TotalProcessed = response.Categorizations.Count,
                HighConfidence = response.Categorizations.Count(c => 
                    c.Suggestions.Any(s => s.Confidence >= 0.8m)),
                MediumConfidence = response.Categorizations.Count(c => 
                    c.Suggestions.Any(s => s.Confidence >= 0.6m && s.Confidence < 0.8m)),
                LowConfidence = response.Categorizations.Count(c => 
                    c.Suggestions.All(s => s.Confidence < 0.6m)),
                AverageConfidence = response.Categorizations.Any() 
                    ? response.Categorizations.Average(c => 
                        c.Suggestions.Any() ? c.Suggestions.Max(s => s.Confidence) : 0)
                    : 0,
                NewRulesGenerated = response.Categorizations.Count(c => c.SuggestedRule != null)
            };
        }
    }
}
