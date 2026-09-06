using Klowee.Api.Contracts.Health;
using Klowee.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class HealthService : IHealthService
{
    private readonly KloweeDbContext _db;

    public HealthService(KloweeDbContext db) => _db = db;

    public async Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        bool canConnect;
        try
        {
            canConnect = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            // A liveness probe must answer even when the database is down.
            canConnect = false;
        }

        return new HealthDto("ok", canConnect ? "connected" : "unreachable", DateTimeOffset.UtcNow);
    }
}
