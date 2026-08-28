namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Enums;

/// <summary>
/// One payment applied to a Sale. Split payments (e.g. part cash, part mobile money) are
/// simply multiple Payment rows on the same Sale whose Amounts sum to Sale.Total.
/// </summary>
public class Payment : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = default!;

    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
}
