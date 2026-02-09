namespace MyMascada.Application.Common.Interfaces;

public interface IDescriptionCleaningService
{
    Task<DescriptionCleaningResponse> CleanDescriptionsAsync(
        IEnumerable<DescriptionCleaningInput> descriptions,
        CancellationToken cancellationToken = default);

    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);
}

public class DescriptionCleaningInput
{
    public int TransactionId { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string? MerchantNameHint { get; set; }
}

public class CleanedDescription
{
    public int TransactionId { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class DescriptionCleaningResponse
{
    public bool Success { get; set; }
    public List<CleanedDescription> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int ProcessingTimeMs { get; set; }
}
