using Klowee.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/[health]")]
public class HealthController : ControllerBase
{
    private readonly KloweeDbContext _db;

    public HealthController(KloweeDbContext db) => _db = db;

    /// <summary>Liveness + database reachability check.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        bool canConnect;
        try
        {
            canConnect = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            canConnect = false;
        }

        return Ok(new
        {
            status = "ok",
            database = canConnect ? "connected" : "unreachable",
            utc = DateTimeOffset.UtcNow
        });
    }
}
