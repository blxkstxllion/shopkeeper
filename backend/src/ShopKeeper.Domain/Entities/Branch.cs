namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

public class Branch : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool IsMainBranch { get; set; }
    public bool IsActive { get; set; } = true;
}
