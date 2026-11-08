using Klowee.Api.Contracts.Reports;

namespace Klowee.Api.Services;

public interface IReportService
{
    /// <summary>The range defaults to the current month so far, in the business time zone.</summary>
    Task<SummaryReportDto> SummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutstandingRowDto>> OutstandingAsync(CancellationToken cancellationToken);

    /// <summary>Twelve rows; the year defaults to the current one.</summary>
    Task<IReadOnlyList<MonthlyRowDto>> MonthlyAsync(int? year, CancellationToken cancellationToken);
}
