using Microsoft.AspNetCore.Mvc;

namespace WhyNoPower.Api.Controllers;

public record PublicAreaStatusDto(string Suburb, string GridStatus, string Note);

/// <summary>
/// The deliberate anonymous/authenticated boundary from the brief: suburb-level aggregates only,
/// no user data. AllowAnonymous is explicit even though the pipeline defaults to authenticated,
/// as a visible marker of intent.
/// </summary>
[ApiController]
[Route("api/public")]
public class PublicStatusController : ControllerBase
{
    [HttpGet("area-status")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public ActionResult<PublicAreaStatusDto> AreaStatus([FromQuery] string suburb)
    {
        // Phase 1 stub — becomes a real query once Phase 2's ESP sync worker lands (ADR-004).
        return new PublicAreaStatusDto(suburb, "No loadshedding", "365+ days suspended nationally.");
    }
}
