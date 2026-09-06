using Klowee.Api.Contracts.Menu;

namespace Klowee.Api.Services;

public interface IAddOnService
{
    Task<IReadOnlyList<AddOnDto>> ListAsync(CancellationToken cancellationToken);
    Task<AddOnDto> CreateAsync(AddOnRequest request, CancellationToken cancellationToken);
    Task<AddOnDto> UpdateAsync(Guid id, AddOnRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
