using Klowee.Api.Contracts.Orders;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;

    public OrdersController(IOrderService orders, IPaymentService payments)
    {
        _orders = orders;
        _payments = payments;
    }

    /// <summary>Newest first. <c>customerId</c> matches orders the customer drank or paid for.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> List(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _orders.ListAsync(new OrderListQuery(from, to, customerId, status, page, pageSize), cancellationToken));

    /// <summary>Items, add-ons, payments, and warnings for items not on the current office menu.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailDto>> Create(OrderRequest request, CancellationToken cancellationToken)
    {
        var created = await _orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Replaces the order. 409 once it has payments: remove them first.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailDto>> Update(Guid id, OrderRequest request, CancellationToken cancellationToken) =>
        Ok(await _orders.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _orders.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Records a payment. 409 if it is more than the order owes. Returns the updated order.</summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailDto>> AddPayment(Guid id, PaymentRequest request, CancellationToken cancellationToken) =>
        Ok(await _payments.AddAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayment(Guid id, Guid paymentId, CancellationToken cancellationToken)
    {
        await _payments.DeleteAsync(id, paymentId, cancellationToken);
        return NoContent();
    }
}
