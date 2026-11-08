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

    [HttpGet("monthly")]
    [ProducesResponseType<IReadOnlyList<MonthlyRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonthlyRowDto>>> Monthly(
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _reports.MonthlyAsync(year, cancellationToken));
}
