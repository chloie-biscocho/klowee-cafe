using Klowee.Api.Contracts.Health;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IHealthService _health;

    public HealthController(IHealthService health) => _health = health;

    /// <summary>Liveness + database reachability check.</summary>
    [HttpGet]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _health.GetHealthAsync(cancellationToken));
}
