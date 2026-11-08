using System.ComponentModel.DataAnnotations;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Customers;

/// <param name="OutstandingBalance">What this customer's tab owes: orders they pay for, minus payments.</param>
public record CustomerSummaryDto(
    Guid Id,
    string Name,
    string? Nickname,
    string? Notes,
    bool IsActive,
    decimal OutstandingBalance,
    int LoyaltyBalance,
    int AvailableRewards,
    DateOnly? LastOrderOn);

public record CustomerDetailDto(
    Guid Id,
    string Name,
    string? Nickname,
    string? Notes,
    bool IsActive,
    decimal OutstandingBalance,
    int LoyaltyBalance,
    int AvailableRewards,
    DateOnly? LastOrderOn,
    IReadOnlyList<OrderSummaryDto> RecentOrders);

public record CustomerRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(80)]
    public string? Nickname { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public bool IsActive { get; init; } = true;
}

public record LoyaltyEntryDto(
    Guid Id,
    int Delta,
    LoyaltyReason Reason,
    Guid? OrderId,
    string? Notes,
    DateTimeOffset CreatedAt);

public record LoyaltyAdjustRequest
{
    [Range(-1000, 1000)]
    public int Delta { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}
