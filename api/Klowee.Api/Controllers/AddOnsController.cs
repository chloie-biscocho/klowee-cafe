using Klowee.Api.Contracts.Menu;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/menu/add-ons")]
public class AddOnsController : ControllerBase
{
    private readonly IAddOnService _addOns;

    public AddOnsController(IAddOnService addOns) => _addOns = addOns;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AddOnDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AddOnDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _addOns.ListAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<AddOnDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddOnDto>> Create(AddOnRequest request, CancellationToken cancellationToken)
    {
        var created = await _addOns.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AddOnDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddOnDto>> Update(
        Guid id,
        AddOnRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _addOns.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _addOns.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
