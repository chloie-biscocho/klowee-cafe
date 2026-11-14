using Klowee.Api.Common;
using Klowee.Api.Contracts.Tabs;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Klowee.Api.Controllers;

/// <summary>
/// A customer's private "my tab" page (decision 021). Anonymous: the token in
/// the path is the key. Every response is no-store and noindex (set for the
/// whole path in Program.cs, errors included), and tokens are never logged.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/tab/{token}")]
public class PublicTabController : ControllerBase
{
    private readonly IPublicTabService _tabs;

    public PublicTabController(IPublicTabService tabs) => _tabs = tabs;

    /// <summary>The page: balance, unpaid orders, loyalty cards, recent payments and claims, how to pay. 404 for any link that isn't live.</summary>
    [HttpGet]
    [EnableRateLimiting(TabRateLimitOptions.ReadPolicy)]
    [ProducesResponseType<PublicTabDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicTabDto>> Get(string token, CancellationToken cancellationToken) =>
        Ok(await _tabs.GetAsync(token, cancellationToken));

    /// <summary>"I've paid": reports a transfer for the owners to confirm.</summary>
    [HttpPost("claims")]
    [EnableRateLimiting(TabRateLimitOptions.ClaimPolicy)]
    [ProducesResponseType<PublicClaimDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublicClaimDto>> Claim(string token, PublicClaimRequest request, CancellationToken cancellationToken) =>
        Ok(await _tabs.ClaimAsync(token, request, cancellationToken));

    /// <summary>Asks for a club's card; nothing changes if they hold it or asked already.</summary>
    [HttpPost("clubs/{clubId:guid}/request")]
    [EnableRateLimiting(TabRateLimitOptions.CardRequestPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestCard(string token, Guid clubId, CancellationToken cancellationToken)
    {
        await _tabs.RequestCardAsync(token, clubId, cancellationToken);
        return NoContent();
    }
}
