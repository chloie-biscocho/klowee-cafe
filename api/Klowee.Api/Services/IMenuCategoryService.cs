using Klowee.Api.Contracts.Menu;

namespace Klowee.Api.Services;

public interface IMenuCategoryService
{
    Task<IReadOnlyList<MenuCategoryDto>> ListAsync(CancellationToken cancellationToken);
    Task<MenuCategoryDto> CreateAsync(MenuCategoryRequest request, CancellationToken cancellationToken);
    Task<MenuCategoryDto> UpdateAsync(Guid id, MenuCategoryRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
