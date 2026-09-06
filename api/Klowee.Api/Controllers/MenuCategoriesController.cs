using Klowee.Api.Contracts.Menu;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/menu/categories")]
public class MenuCategoriesController : ControllerBase
{
    private readonly IMenuCategoryService _categories;

    public MenuCategoriesController(IMenuCategoryService categories) => _categories = categories;

    /// <summary>Lists categories in display order.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MenuCategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MenuCategoryDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _categories.ListAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<MenuCategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuCategoryDto>> Create(
        MenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _categories.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MenuCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuCategoryDto>> Update(
        Guid id,
        MenuCategoryRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _categories.UpdateAsync(id, request, cancellationToken));

    /// <summary>Soft-deletes a category. Rejected while live menu items still use it.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _categories.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
