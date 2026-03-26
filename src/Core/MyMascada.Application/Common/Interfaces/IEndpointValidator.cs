namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Validates user-provided API endpoint URLs to prevent SSRF attacks.
/// </summary>
public interface IEndpointValidator
{
    /// <summary>
    /// Validates that a URL is safe to use as an AI API endpoint.
    /// Resolves the hostname and checks that the IP is not in a blocked range.
    /// </summary>
    /// <returns>A validation result indicating success or the reason for rejection.</returns>
    Task<EndpointValidationResult> ValidateEndpointAsync(string url);
}

public class EndpointValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }

    public static EndpointValidationResult Valid() => new() { IsValid = true };
    public static EndpointValidationResult Invalid(string error) => new() { IsValid = false, Error = error };
}
