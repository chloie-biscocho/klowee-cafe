using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

/// <summary>The loyalty cards (clubs): their slots, rewards and the categories that feed them. Decision 020.</summary>
[ApiController]
[Route("api/loyalty/clubs")]
public class LoyaltyClubsController : ControllerBase
{
    private readonly ILoyaltyClubService _clubs;

    public LoyaltyClubsController(ILoyaltyClubService clubs) => _clubs = clubs;

    /// <summary>Every club, active or not, in display order, with its member count.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LoyaltyClubDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoyaltyClubDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _clubs.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<LoyaltyClubDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoyaltyClubDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _clubs.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<LoyaltyClubDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoyaltyClubDto>> Create(LoyaltyClubRequest request, CancellationToken cancellationToken)
    {
        var created = await _clubs.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Replaces the club, its rewards (matched by slot) and, when sent, its categories.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<LoyaltyClubDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoyaltyClubDto>> Update(Guid id, LoyaltyClubRequest request, CancellationToken cancellationToken) =>
        Ok(await _clubs.UpdateAsync(id, request, cancellationToken));

    /// <summary>Starts the club; the first activation dates it (orders before that day never stamp it).</summary>
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType<LoyaltyClubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoyaltyClubDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _clubs.SetActiveAsync(id, true, cancellationToken));

    /// <summary>Stops the club earning and hides it on customers; its ledger stays.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType<LoyaltyClubDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoyaltyClubDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _clubs.SetActiveAsync(id, false, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _clubs.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
