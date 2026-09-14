using Klowee.Api.Contracts.Public;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly IPublicHomeService _home;

    public PublicController(IPublicHomeService home) => _home = home;

    /// <summary>
    /// Everything the marketing site renders, in one anonymous request:
    /// settings, the current announcement, the next event, the live pop-up menu,
    /// the active packages, and the published events.
    /// </summary>
    [HttpGet("home")]
    [ProducesResponseType<PublicHomeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicHomeDto>> Home(CancellationToken cancellationToken)
    {
        // A minute of shared caching. Long enough for a burst of visitors to hit
        // a cache rather than the database, short enough that an owner who
        // publishes a menu sees it on the site within a minute.
        Response.Headers.CacheControl = "public, max-age=60";

        return Ok(await _home.GetHomeAsync(cancellationToken));
    }
}
