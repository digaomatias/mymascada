using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlags _featureFlags;

    public FeaturesController(IFeatureFlags featureFlags)
    {
        _featureFlags = featureFlags;
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
            EmailNotifications = _featureFlags.EmailNotifications
        };

        return Ok(response);
    }
}

public class FeatureFlagsResponse
{
    public bool AiCategorization { get; set; }
    public bool GoogleOAuth { get; set; }
    public bool BankSync { get; set; }
    public bool EmailNotifications { get; set; }
}
