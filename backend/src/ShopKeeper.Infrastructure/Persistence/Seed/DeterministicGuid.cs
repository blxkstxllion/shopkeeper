namespace ShopKeeper.Infrastructure.Persistence.Seed;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Produces a stable Guid from a string so HasData seed rows (e.g. the Permission
/// catalog) get the same Id every time migrations are regenerated, instead of a
/// random one that would produce spurious diffs.
/// </summary>
public static class DeterministicGuid
{
    public static Guid Create(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
