namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for beta access control.
/// When RequireInviteCode is true, new registrations must provide a valid invite code.
/// </summary>
public class BetaAccessOptions
{
    public const string SectionName = "BetaAccess";

    /// <summary>
    /// Whether registration requires a valid invite code.
    /// </summary>
    public bool RequireInviteCode { get; set; } = false;

    /// <summary>
    /// Comma-separated list of valid invite codes.
    /// </summary>
    public string ValidInviteCodes { get; set; } = string.Empty;

    /// <summary>
    /// Gets the parsed list of valid invite codes.
    /// </summary>
    public IReadOnlyList<string> GetValidCodes()
    {
        if (string.IsNullOrWhiteSpace(ValidInviteCodes))
            return Array.Empty<string>();

        return ValidInviteCodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList()
            .AsReadOnly();
    }
}
