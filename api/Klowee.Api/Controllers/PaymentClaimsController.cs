using Klowee.Api.Contracts.Tabs;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

/// <summary>Customers' "I've paid" reports from their tab page, for the owners to confirm or reject (decision 021).</summary>
[ApiController]
[Route("api/payment-claims")]
public class PaymentClaimsController : ControllerBase
{
    private readonly IPaymentClaimService _claims;

    public PaymentClaimsController(IPaymentClaimService claims) => _claims = claims;

    /// <summary>Newest first, with each customer's name and what their tab owes now.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentClaimDto>>> List([FromQuery] PaymentClaimStatus? status, CancellationToken cancellationToken) =>
        Ok(await _claims.ListAsync(status, cancellationToken));

    /// <summary>Settles the payer's tab with the claim (oldest order first) and marks it confirmed; 409 if it's now more than the tab owes.</summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<PaymentClaimDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentClaimDto>> Confirm(Guid id, ConfirmClaimRequest request, CancellationToken cancellationToken) =>
        Ok(await _claims.ConfirmAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<PaymentClaimDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentClaimDto>> Reject(Guid id, RejectClaimRequest request, CancellationToken cancellationToken) =>
        Ok(await _claims.RejectAsync(id, request, cancellationToken));
}

/// <summary>The accounts customers pay into from their tab page.</summary>
[ApiController]
[Route("api/payment-methods")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _methods;

    public PaymentMethodsController(IPaymentMethodService methods) => _methods = methods;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _methods.ListAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PaymentMethodDto>> Create(PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var created = await _methods.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentMethodDto>> Update(Guid id, PaymentMethodRequest request, CancellationToken cancellationToken) =>
        Ok(await _methods.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult<PaymentMethodDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _methods.SetActiveAsync(id, true, cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult<PaymentMethodDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _methods.SetActiveAsync(id, false, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _methods.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

/// <summary>Cards customers asked for on their tab page.</summary>
[ApiController]
[Route("api/loyalty")]
public class CardRequestsController : ControllerBase
{
    private readonly ICardRequestService _requests;

    public CardRequestsController(ICardRequestService requests) => _requests = requests;

    /// <summary>Requested memberships, oldest first, with customer, card and date.</summary>
    [HttpGet("card-requests")]
    public async Task<ActionResult<IReadOnlyList<CardRequestDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _requests.ListAsync(cancellationToken));

    /// <summary>Gives the card: active from today (business time zone).</summary>
    [HttpPost("memberships/{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        await _requests.ApproveAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Removes the request.</summary>
    [HttpPost("memberships/{id:guid}/decline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken)
    {
        await _requests.DeclineAsync(id, cancellationToken);
        return NoContent();
    }
}
