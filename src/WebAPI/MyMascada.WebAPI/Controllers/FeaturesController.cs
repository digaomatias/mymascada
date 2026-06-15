using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlags _featureFlags;
    private readonly IUserFeatureService _userFeatures;
    private readonly ICurrentUserService _currentUser;

    public FeaturesController(
        IFeatureFlags featureFlags,
        IUserFeatureService userFeatures,
        ICurrentUserService currentUser)
    {
        _featureFlags = featureFlags;
        _userFeatures = userFeatures;
        _currentUser = currentUser;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<FeatureFlagsResponse> GetFeatures()
    {
        var response = new FeatureFlagsResponse
        {
            AiCategorization = _featureFlags.AiCategorization,
            GoogleOAuth = _featureFlags.GoogleOAuth,
            BankSync = _featureFlags.BankSync,
            EmailNotifications = _featureFlags.EmailNotifications,
            StripeBilling = _featureFlags.StripeBilling
        };

        return Ok(response);
    }

    /// <summary>
    /// Per-user feature toggles for the authenticated caller. The client uses this
    /// to gate UI (Ask Alce, receipt scan, bank connections). Each value is already
    /// AND-ed with the global capability, so the client can trust it directly.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyDictionary<string, bool>>> GetMyFeatures(
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var resolved = await _userFeatures.GetResolvedAsync(userId, cancellationToken);
        return Ok(resolved);
    }
}

public class FeatureFlagsResponse
{
    public bool AiCategorization { get; set; }
    public bool GoogleOAuth { get; set; }
    public bool BankSync { get; set; }
    public bool EmailNotifications { get; set; }
    public bool StripeBilling { get; set; }
}
