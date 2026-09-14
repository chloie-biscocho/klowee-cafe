using Klowee.Api.Contracts.Announcements;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;

    public AnnouncementsController(IAnnouncementService announcements) => _announcements = announcements;

    /// <summary>Lists announcements newest-window-first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnnouncementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _announcements.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _announcements.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnnouncementDto>> Create(
        AnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _announcements.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Update(
        Guid id,
        AnnouncementRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _announcements.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _announcements.SetActiveAsync(id, isActive: true, cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _announcements.SetActiveAsync(id, isActive: false, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _announcements.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
