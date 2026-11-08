using Klowee.Api.Contracts.Reports;

namespace Klowee.Api.Services;

public interface IReportService
{
    /// <summary>The range defaults to the current month so far, in the business time zone.</summary>
    Task<SummaryReportDto> SummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutstandingRowDto>> OutstandingAsync(CancellationToken cancellationToken);

    /// <summary>Twelve rows; the year defaults to the current one.</summary>
    Task<IReadOnlyList<MonthlyRowDto>> MonthlyAsync(int? year, CancellationToken cancellationToken);

    /// <summary>One row per month from <paramref name="from"/> to <paramref name="to"/> (first days of months), empty months as zero. No <paramref name="from"/> starts at the first month with any order.</summary>
    Task<IReadOnlyList<MonthlyRowDto>> MonthlyRangeAsync(DateOnly? from, DateOnly to, CancellationToken cancellationToken);
}
