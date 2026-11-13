using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;
    private readonly ILoyaltyService _loyalty;
    private readonly IPaymentService _payments;

    public CustomersController(ICustomerService customers, ILoyaltyService loyalty, IPaymentService payments)
    {
        _customers = customers;
        _loyalty = loyalty;
        _payments = payments;
    }

    /// <summary>Customers by name, each with their tab balance, card on every active club, and last order date.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerSummaryDto>>> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _customers.ListAsync(includeInactive, q, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _customers.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDetailDto>> Create(CustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await _customers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDetailDto>> Update(Guid id, CustomerRequest request, CancellationToken cancellationToken) =>
        Ok(await _customers.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDetailDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _customers.SetActiveAsync(id, true, cancellationToken));

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDetailDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await _customers.SetActiveAsync(id, false, cancellationToken));

    /// <summary>Refused (409) while the customer has orders; deactivate instead.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _customers.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// For duplicate names from the spreadsheet: moves this customer's orders
    /// (with their payments), payer references, stamps and used rewards to the
    /// target, then deletes this customer. Returns the target.
    /// </summary>
    [HttpPost("{id:guid}/merge-into/{targetId:guid}")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailDto>> Merge(Guid id, Guid targetId, CancellationToken cancellationToken) =>
        Ok(await _customers.MergeAsync(id, targetId, cancellationToken));

    /// <summary>The stamp ledger, newest first; one club's entries with <c>clubId</c>.</summary>
    [HttpGet("{id:guid}/loyalty")]
    [ProducesResponseType<IReadOnlyList<LoyaltyEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LoyaltyEntryDto>>> Loyalty(
        Guid id,
        [FromQuery] Guid? clubId,
        CancellationToken cancellationToken) =>
        Ok(await _loyalty.LedgerAsync(id, clubId, cancellationToken));

    /// <summary>Adds or removes stamps on one club by hand (an Adjustment entry).</summary>
    [HttpPost("{id:guid}/loyalty/adjust")]
    [ProducesResponseType<LoyaltyEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoyaltyEntryDto>> Adjust(Guid id, LoyaltyAdjustRequest request, CancellationToken cancellationToken) =>
        Ok(await _loyalty.AdjustAsync(id, request, cancellationToken));

    /// <summary>Marks an available reward used without an order (the paper card says it was used).</summary>
    [HttpPost("{id:guid}/loyalty/redemptions")]
    [ProducesResponseType<LoyaltyRedemptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoyaltyRedemptionDto>> Redeem(Guid id, LoyaltyRedemptionRequest request, CancellationToken cancellationToken) =>
        Ok(await _loyalty.RedeemAsync(id, request, cancellationToken));

    /// <summary>Makes a used reward available again. Refused (409) when an order used it.</summary>
    [HttpDelete("{id:guid}/loyalty/redemptions/{redemptionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unredeem(Guid id, Guid redemptionId, CancellationToken cancellationToken)
    {
        await _loyalty.UnredeemAsync(id, redemptionId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// "Paid 500 for last month": spreads the amount over the customer's tab,
    /// oldest order first, one payment per order touched. Returns those payments.
    /// </summary>
    [HttpPost("{id:guid}/settle")]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> Settle(Guid id, PaymentRequest request, CancellationToken cancellationToken) =>
        Ok(await _payments.SettleAsync(id, request, cancellationToken));
}
