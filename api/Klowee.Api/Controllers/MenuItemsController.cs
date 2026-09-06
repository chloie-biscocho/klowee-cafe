using Klowee.Api.Contracts.Menu;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/menu/items")]
public class MenuItemsController : ControllerBase
{
    private readonly IMenuItemService _items;

    public MenuItemsController(IMenuItemService items) => _items = items;

    /// <summary>Lists menu items with their category name.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MenuItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> List(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _items.ListAsync(includeArchived, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _items.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuItemDto>> Create(
        MenuItemRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _items.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuItemDto>> Update(
        Guid id,
        MenuItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _items.UpdateAsync(id, request, cancellationToken));

    /// <summary>Hides the item from new menus without touching past versions.</summary>
    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> Archive(Guid id, CancellationToken cancellationToken) =>
        Ok(await _items.SetArchivedAsync(id, isArchived: true, cancellationToken));

    [HttpPatch("{id:guid}/unarchive")]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> Unarchive(Guid id, CancellationToken cancellationToken) =>
        Ok(await _items.SetArchivedAsync(id, isArchived: false, cancellationToken));

    /// <summary>Soft-deletes an item. Rejected while any menu version prices it.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _items.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
