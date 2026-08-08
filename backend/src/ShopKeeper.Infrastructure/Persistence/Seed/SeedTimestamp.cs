namespace ShopKeeper.Infrastructure.Persistence.Seed;

/// <summary>Fixed timestamp for HasData seed rows so regenerating migrations doesn't produce a diff every time.</summary>
public static class SeedTimestamp
{
    public static readonly DateTimeOffset Value = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
