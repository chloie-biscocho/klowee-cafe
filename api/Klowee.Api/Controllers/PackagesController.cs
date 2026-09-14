using Klowee.Api.Contracts.Packages;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/packages")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packages;

    public PackagesController(IPackageService packages) => _packages = packages;

    /// <summary>Lists packages in display order, each with its inclusions.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PackageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PackageDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _packages.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PackageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _packages.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PackageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PackageDto>> Create(
        PackageRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _packages.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Replaces the package and its whole inclusion list.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PackageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PackageDto>> Update(
        Guid id,
        PackageRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _packages.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType<PackageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _packages.SetActiveAsync(id, isActive: true, cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType<PackageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _packages.SetActiveAsync(id, isActive: false, cancellationToken));

    /// <summary>Soft-deletes a package and its inclusions.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _packages.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
