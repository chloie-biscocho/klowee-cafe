using Klowee.Api.Contracts.Public;

namespace Klowee.Api.Services;

public interface IPublicHomeService
{
    Task<PublicHomeDto> GetHomeAsync(CancellationToken cancellationToken);
}
