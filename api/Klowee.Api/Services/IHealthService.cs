using Klowee.Api.Contracts.Health;

namespace Klowee.Api.Services;

public interface IHealthService
{
    Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken);
}
