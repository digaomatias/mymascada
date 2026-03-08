using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
public class VersionController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<VersionResponse> GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;

        return Ok(new VersionResponse
        {
            Version = version?.ToString(3) ?? "0.0.0"
        });
    }
}

public class VersionResponse
{
    public string Version { get; set; } = string.Empty;
}
