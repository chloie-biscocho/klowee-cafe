using Klowee.Api.Contracts.Menu;
using Klowee.Api.Entities;

namespace Klowee.Api.Services;

public interface IMenuVersionService
{
    Task<IReadOnlyList<MenuVersionSummaryDto>> ListAsync(MenuContext? context, CancellationToken cancellationToken);
    Task<MenuVersionDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The id of the version that is live for this context right now, or null.</summary>
    Task<Guid?> FindCurrentIdAsync(MenuContext context, CancellationToken cancellationToken);

    Task<MenuVersionDetailDto> GetCurrentAsync(MenuContext context, CancellationToken cancellationToken);
    Task<MenuVersionDetailDto> CreateAsync(CreateMenuVersionRequest request, CancellationToken cancellationToken);
    Task<MenuVersionDetailDto> UpdateAsync(Guid id, UpdateMenuVersionRequest request, CancellationToken cancellationToken);
    Task<MenuVersionDetailDto> SetPublishedAsync(Guid id, bool isPublished, CancellationToken cancellationToken);
    Task<MenuVersionDetailDto> ReplaceItemsAsync(Guid id, IReadOnlyList<MenuVersionItemRequest> items, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
