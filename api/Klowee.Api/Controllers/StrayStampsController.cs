using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

/// <summary>
/// Handover 17a: stamps given by mistake to carried-over cards (an order already
/// on the paper card, paid later). List first; void only when the owner says so.
/// </summary>
[ApiController]
[Route("api/loyalty/stray-stamps")]
public class StrayStampsController : ControllerBase
{
    private readonly IStrayStampService _stray;

    public StrayStampsController(IStrayStampService stray) => _stray = stray;

    /// <summary>Read only: each stray stamp, each card's position before and after, rewards already used that depended on them.</summary>
    [HttpGet]
    public async Task<ActionResult<StrayStampsDto>> List(CancellationToken cancellationToken) =>
        Ok(await _stray.ListAsync(cancellationToken));

    /// <summary>Voids them through the normal reconcile, in one transaction, and returns what is left (nothing). Money never moves.</summary>
    [HttpPost("void")]
    public async Task<ActionResult<StrayStampsDto>> Void(CancellationToken cancellationToken) =>
        Ok(await _stray.VoidAsync(cancellationToken));
}
