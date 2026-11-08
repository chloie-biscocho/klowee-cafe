using Klowee.Api.Common;
using Klowee.Api.Contracts.Reports;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

/// <summary>Read-only figures for the admin dashboard.</summary>
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>Defaults to the current month so far.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<SummaryReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryReportDto>> Summary(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _reports.SummaryAsync(from, to, cancellationToken));

    [HttpGet("outstanding")]
    [ProducesResponseType<IReadOnlyList<OutstandingRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OutstandingRowDto>>> Outstanding(CancellationToken cancellationToken) =>
        Ok(await _reports.OutstandingAsync(cancellationToken));

    /// <summary>
    /// <c>?year=2026</c>: the twelve months of that year (the default is this year).
    /// <c>?from=2025-05&amp;to=2026-11</c>: one row per month in the range; leave out
    /// <c>from</c> to start at the first month with any order.
    /// </summary>
    [HttpGet("monthly")]
    [ProducesResponseType<IReadOnlyList<MonthlyRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonthlyRowDto>>> Monthly(
        [FromQuery] int? year = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken cancellationToken = default)
    {
        if (from is null && to is null)
        {
            return Ok(await _reports.MonthlyAsync(year, cancellationToken));
        }

        if (year is not null)
        {
            throw new ValidationFailedException("Use either 'year' or 'from'/'to', not both.");
        }

        if (to is null)
        {
            throw new ValidationFailedException("'to' is required with 'from'.");
        }

        return Ok(await _reports.MonthlyRangeAsync(from is null ? null : ParseMonth(from, "from"), ParseMonth(to, "to"), cancellationToken));
    }

    private static DateOnly ParseMonth(string value, string name) =>
        DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month)
            ? month
            : throw new ValidationFailedException($"'{name}' must be a month as YYYY-MM.");
}
