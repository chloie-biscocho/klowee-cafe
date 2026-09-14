using Klowee.Api.Contracts.Events;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;

    public EventsController(IEventService events) => _events = events;

    /// <summary>Lists events newest-first, optionally filtered by status and published state.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EventSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> List(
        [FromQuery] EventStatus? status,
        [FromQuery] bool? published,
        CancellationToken cancellationToken) =>
        Ok(await _events.ListAsync(status, published, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _events.GetAsync(id, cancellationToken));

    /// <summary>Creates an unpublished event.</summary>
    [HttpPost]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EventDetailDto>> Create(
        EventRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _events.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> Update(
        Guid id,
        EventRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _events.UpdateAsync(id, request, cancellationToken));

    /// <summary>Publishing requires the status to be Upcoming or Done.</summary>
    [HttpPatch("{id:guid}/publish")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventDetailDto>> Publish(Guid id, CancellationToken cancellationToken) =>
        Ok(await _events.SetPublishedAsync(id, isPublished: true, cancellationToken));

    [HttpPatch("{id:guid}/unpublish")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> Unpublish(Guid id, CancellationToken cancellationToken) =>
        Ok(await _events.SetPublishedAsync(id, isPublished: false, cancellationToken));

    /// <summary>Replaces the whole photo list in one transaction.</summary>
    [HttpPut("{id:guid}/photos")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> ReplacePhotos(
        Guid id,
        List<EventPhotoRequest> photos,
        CancellationToken cancellationToken) =>
        Ok(await _events.ReplacePhotosAsync(id, photos, cancellationToken));

    /// <summary>Soft-deletes an event and its photos.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _events.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
