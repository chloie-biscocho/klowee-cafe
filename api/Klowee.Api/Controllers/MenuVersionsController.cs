using Klowee.Api.Contracts.Menu;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/menu/versions")]
public class MenuVersionsController : ControllerBase
{
    private readonly IMenuVersionService _versions;

    public MenuVersionsController(IMenuVersionService versions) => _versions = versions;

    /// <summary>Lists versions newest-effective-first, optionally filtered by context.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MenuVersionSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MenuVersionSummaryDto>>> List(
        [FromQuery] MenuContext? context,
        CancellationToken cancellationToken) =>
        Ok(await _versions.ListAsync(context, cancellationToken));

    /// <summary>
    /// The menu the public site should show: the published version for this
    /// context with the latest effective date on or before today.
    /// </summary>
    [HttpGet("current")]
    [AllowAnonymous]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuVersionDetailDto>> Current(
        [FromQuery] MenuContext context,
        CancellationToken cancellationToken) =>
        Ok(await _versions.GetCurrentAsync(context, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuVersionDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _versions.GetAsync(id, cancellationToken));

    /// <summary>Creates a draft version, optionally copying another version's items.</summary>
    [HttpPost]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuVersionDetailDto>> Create(
        CreateMenuVersionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _versions.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuVersionDetailDto>> Update(
        Guid id,
        UpdateMenuVersionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _versions.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/publish")]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuVersionDetailDto>> Publish(Guid id, CancellationToken cancellationToken) =>
        Ok(await _versions.SetPublishedAsync(id, isPublished: true, cancellationToken));

    [HttpPatch("{id:guid}/unpublish")]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuVersionDetailDto>> Unpublish(Guid id, CancellationToken cancellationToken) =>
        Ok(await _versions.SetPublishedAsync(id, isPublished: false, cancellationToken));

    /// <summary>Replaces the whole item list for a draft version in one transaction.</summary>
    [HttpPut("{id:guid}/items")]
    [ProducesResponseType<MenuVersionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuVersionDetailDto>> ReplaceItems(
        Guid id,
        List<MenuVersionItemRequest> items,
        CancellationToken cancellationToken) =>
        Ok(await _versions.ReplaceItemsAsync(id, items, cancellationToken));

    /// <summary>Soft-deletes a draft version and its items.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _versions.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
