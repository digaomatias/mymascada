using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/description-cleaning")]
public class DescriptionCleaningController : ControllerBase
{
    private readonly IDescriptionCleaningService _descriptionCleaningService;
    private readonly ILogger<DescriptionCleaningController> _logger;

    public DescriptionCleaningController(
        IDescriptionCleaningService descriptionCleaningService,
        ILogger<DescriptionCleaningController> logger)
    {
        _descriptionCleaningService = descriptionCleaningService;
        _logger = logger;
    }

    /// <summary>
    /// Preview cleaned descriptions for raw bank transaction descriptions.
    /// Calls IDescriptionCleaningService directly (no Hangfire). For the reconciliation preview feature.
    /// Maximum 10 descriptions per request.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<DescriptionCleaningPreviewResponse>> PreviewCleanedDescriptions(
        [FromBody] DescriptionCleaningPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Descriptions.Count > 10)
        {
            return BadRequest(new { error = "Maximum 10 descriptions per preview request" });
        }

        if (!request.Descriptions.Any())
        {
            return BadRequest(new { error = "At least one description is required" });
        }

        // Validate individual description lengths to prevent abuse
        const int maxDescriptionLength = 500;
        foreach (var desc in request.Descriptions)
        {
            if (desc.RawDescription.Length > maxDescriptionLength)
                desc.RawDescription = desc.RawDescription[..maxDescriptionLength];
            if (desc.MerchantNameHint?.Length > maxDescriptionLength)
                desc.MerchantNameHint = desc.MerchantNameHint[..maxDescriptionLength];
        }

        var inputs = request.Descriptions.Select((d, i) => new DescriptionCleaningInput
        {
            TransactionId = i, // Synthetic ID for mapping
            OriginalDescription = d.RawDescription,
            MerchantNameHint = d.MerchantNameHint
        }).ToList();

        var result = await _descriptionCleaningService.CleanDescriptionsAsync(inputs, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Description cleaning preview failed: {Errors}",
                string.Join(", ", result.Errors));
            return BadRequest(new { errors = result.Errors });
        }

        var response = new DescriptionCleaningPreviewResponse
        {
            Results = result.Results.Select(r => new DescriptionCleaningPreviewResult
            {
                RawDescription = r.OriginalDescription,
                CleanedDescription = r.Description,
                Confidence = r.Confidence
            }).ToList()
        };

        return Ok(response);
    }
}

public class DescriptionCleaningPreviewRequest
{
    public List<DescriptionCleaningPreviewItem> Descriptions { get; set; } = new();
}

public class DescriptionCleaningPreviewItem
{
    public string RawDescription { get; set; } = string.Empty;
    public string? MerchantNameHint { get; set; }
}

public class DescriptionCleaningPreviewResponse
{
    public List<DescriptionCleaningPreviewResult> Results { get; set; } = new();
}

public class DescriptionCleaningPreviewResult
{
    public string RawDescription { get; set; } = string.Empty;
    public string CleanedDescription { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
}
