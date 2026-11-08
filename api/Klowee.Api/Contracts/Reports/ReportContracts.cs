using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Reports;

public record ChannelTotalDto(PaymentChannel Channel, decimal Amount, int Count);

public record TopItemDto(Guid MenuItemId, string Name, int Quantity);

/// <param name="Revenue">Totals of orders with ordered_on in the range.</param>
/// <param name="Collected">Payments with paid_on in the range, whenever the order was.</param>
/// <param name="Outstanding">Everything owed today, all time.</param>
/// <param name="NewCustomers">Customers whose first order falls in the range.</param>
public record SummaryReportDto(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    decimal Collected,
    decimal Outstanding,
    int OrderCount,
    int DrinkCount,
    IReadOnlyList<ChannelTotalDto> ByChannel,
    IReadOnlyList<TopItemDto> TopItems,
    int NewCustomers);

public record OutstandingRowDto(
    Guid CustomerId,
    string Name,
    decimal Balance,
    DateOnly OldestUnpaidOn,
    int OrderCount);

/// <param name="Year">Added with the from/to form, so a range spanning years is unambiguous.</param>
public record MonthlyRowDto(int Year, int Month, decimal Revenue, decimal Collected, int Orders, int Drinks);
