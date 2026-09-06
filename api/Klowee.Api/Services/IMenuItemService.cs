using Klowee.Api.Contracts.Menu;

namespace Klowee.Api.Services;

public interface IMenuItemService
{
    Task<IReadOnlyList<MenuItemDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<MenuItemDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<MenuItemDto> CreateAsync(MenuItemRequest request, CancellationToken cancellationToken);
    Task<MenuItemDto> UpdateAsync(Guid id, MenuItemRequest request, CancellationToken cancellationToken);
    Task<MenuItemDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
