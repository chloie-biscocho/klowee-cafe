using System.ComponentModel.DataAnnotations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Orders;

/// <summary>Derived from the payments on every read; never stored (decision 016).</summary>
public enum PaymentStatus
{
    Unpaid,
    Partial,
    Paid
}

public record OrderLineRequest
{
    [Required]
    public Guid MenuVersionItemId { get; init; }

    [Range(1, 100)]
    public int Quantity { get; init; } = 1;

    public List<Guid> AddOnIds { get; init; } = [];

    /// <summary>A loyalty reward for one unit of this line; it must be available to the drinker for the drink's club.</summary>
    public Guid? RewardId { get; init; }
}

/// <summary>A whole order. A PUT replaces the items in full, and is refused once the order has payments.</summary>
public record OrderRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    /// <summary>Who pays, when it is someone else ("Prince c/o Kee"). Omit when the customer pays.</summary>
    public Guid? PayerId { get; init; }

    [Required]
    public DateOnly OrderedOn { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    /// <summary>None, Percent or Fixed. LoyaltyFree is history only and refused (rewards now sit on lines).</summary>
    public DiscountType DiscountType { get; init; } = DiscountType.None;

    /// <summary>Percent 0–100, or pesos off (0 to the subtotal). Ignored for None.</summary>
    [Range(0, 1000000)]
    public decimal DiscountValue { get; init; }

    [MinLength(1, ErrorMessage = "An order needs at least one item.")]
    public List<OrderLineRequest> Items { get; init; } = [];
}

public record PersonRef(Guid Id, string Name);

public record OrderItemAddOnDto(Guid Id, Guid AddOnId, string Name, decimal UnitPrice);

/// <param name="AmountOff">What the reward took off this line (₱0 for an add-on given free that the line didn't carry).</param>
public record OrderLineRewardDto(Guid RewardId, string Label, LoyaltyRewardType Type, decimal AmountOff);

public record OrderItemDto(
    Guid Id,
    Guid MenuVersionItemId,
    Guid MenuItemId,
    string Name,
    string MenuVersionName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    IReadOnlyList<OrderItemAddOnDto> AddOns,
    OrderLineRewardDto? Reward);

public record PaymentDto(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    PaymentChannel Channel,
    DateOnly PaidOn,
    string? Reference,
    string? Notes);

/// <param name="Items">The drinks, as the owners would say them: "Spanish Latte ×2, Americano".</param>
public record OrderSummaryDto(
    Guid Id,
    DateOnly OrderedOn,
    PersonRef Customer,
    PersonRef? Payer,
    int ItemCount,
    string Items,
    decimal Total,
    decimal Paid,
    PaymentStatus Status);

public record OrderDetailDto(
    Guid Id,
    OrderContext Context,
    DateOnly OrderedOn,
    PersonRef Customer,
    PersonRef? Payer,
    string? Notes,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal Paid,
    decimal Balance,
    PaymentStatus Status,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<string> Warnings);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record PaymentRequest
{
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Amount { get; init; }

    public PaymentChannel Channel { get; init; }

    [Required]
    public DateOnly PaidOn { get; init; }

    [MaxLength(120)]
    public string? Reference { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
