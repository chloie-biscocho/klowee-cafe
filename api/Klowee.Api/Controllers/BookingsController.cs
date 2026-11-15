using Klowee.Api.Common;
using Klowee.Api.Contracts.Bookings;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Klowee.Api.Controllers;

/// <summary>
/// The homepage's booking form (handover 20, decision 023). Anonymous, rate
/// limited by client IP, never cached. The answer is an acknowledgement only:
/// the stored request is never echoed back.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/bookings")]
public class PublicBookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public PublicBookingsController(IBookingService bookings) => _bookings = bookings;

    [HttpPost]
    [EnableRateLimiting(BookingRateLimitOptions.PolicyName)]
    [ProducesResponseType<PublicBookingReceipt>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(PublicBookingRequest request, CancellationToken cancellationToken)
    {
        await _bookings.SubmitAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PublicBookingReceipt(true));
    }
}

/// <summary>Booking requests, for the owners.</summary>
[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings) => _bookings = bookings;

    /// <summary>Newest first, optionally one status.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BookingSummaryDto>>> List(
        [FromQuery] BookingStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _bookings.ListAsync(status, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _bookings.GetAsync(id, cancellationToken));

    /// <summary>The status and the owners' notes, always both. A status change records who and when.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Update(Guid id, UpdateBookingRequest request, CancellationToken cancellationToken) =>
        Ok(await _bookings.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _bookings.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
